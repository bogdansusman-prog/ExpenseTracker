import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { I18n } from '../../i18n/i18n.service';
import { TranslatePipe } from '../../i18n/translate.pipe';
import { AxMascot } from '../../components/ax-mascot/ax-mascot';
import { BankApiService } from '../../services/bank-api.service';
import { ExpenseApiService } from '../../services/expense-api.service';
import { Category } from '../../models/category.model';
import { BankConnection, BankOption, BankPreviewItem, BankStatus } from '../../models/bank.model';
import { formatMoney } from '../../shared/format';

/** One preview row with what the user decided for it. */
interface PreviewRow extends BankPreviewItem {
  selected: boolean;
  categoryId: number | null;
}

@Component({
  selector: 'app-bank',
  imports: [FormsModule, RouterLink, TranslatePipe, AxMascot],
  templateUrl: './bank.html',
  styleUrl: './bank.scss'
})
export class Bank implements OnInit {
  private readonly api = inject(BankApiService);
  private readonly expenseApi = inject(ExpenseApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly i18n = inject(I18n);

  readonly formatMoney = formatMoney;

  readonly status = signal<BankStatus | null>(null);
  readonly banks = signal<BankOption[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly selectedBank = signal('');

  readonly loading = signal(true);
  readonly connecting = signal(false);
  readonly busyConnectionId = signal<number | null>(null);
  readonly importing = signal(false);

  readonly message = signal('');
  readonly error = signal('');

  /** Preview of bank transactions waiting for confirmation. */
  readonly previewConnection = signal<BankConnection | null>(null);
  readonly previewRange = signal<{ from: string; to: string } | null>(null);
  readonly rows = signal<PreviewRow[]>([]);
  readonly periodDays = signal<number | null>(null);

  readonly newRows = computed(() => this.rows().filter(row => !row.alreadyImported));
  readonly selectedRows = computed(() => this.rows().filter(row => row.selected));
  readonly missingCategory = computed(() => this.selectedRows().filter(row => row.categoryId === null).length);
  readonly canImport = computed(() => this.selectedRows().length > 0 && this.missingCategory() === 0 && !this.importing());

  ngOnInit(): void {
    this.expenseApi.getCategories().subscribe({ next: categories => this.categories.set(categories) });

    const connected = Number(this.route.snapshot.queryParamMap.get('connected'));
    this.load(connected > 0 ? connected : null);
  }

  load(autoPreviewId: number | null = null): void {
    this.loading.set(true);

    this.api.getStatus().subscribe({
      next: status => {
        this.status.set(status);
        this.loading.set(false);

        if (status.configured && this.banks().length === 0) {
          this.loadBanks();
        }

        const connection = autoPreviewId ? status.connections.find(item => item.id === autoPreviewId) : null;
        if (connection) {
          this.message.set('Banca a fost conectata. Iata tranzactiile din ultimele 90 de zile.');
          this.router.navigate([], { queryParams: {}, replaceUrl: true });
          this.sync(connection);
        }
      },
      error: error => {
        this.loading.set(false);
        this.error.set(this.describe(error));
      }
    });
  }

  private loadBanks(): void {
    this.api.getBanks('RO').subscribe({
      next: banks => {
        this.banks.set(banks);
        const bcr = banks.find(bank => /bcr|comerciala romana/i.test(bank.name));
        this.selectedBank.set((bcr ?? banks[0])?.name ?? '');
      },
      error: error => this.error.set(this.describe(error))
    });
  }

  connect(): void {
    if (!this.selectedBank()) {
      return;
    }

    this.clearAlerts();
    this.connecting.set(true);

    this.api.connect(this.selectedBank()).subscribe({
      // Leave the app: the bank (e.g. George) asks you to approve read-only access, then sends you back.
      next: response => (window.location.href = response.url),
      error: error => {
        this.connecting.set(false);
        this.error.set(this.describe(error));
      }
    });
  }

  sync(connection: BankConnection): void {
    this.clearAlerts(false);
    this.busyConnectionId.set(connection.id);

    this.api.preview(connection.id, this.periodDays()).subscribe({
      next: preview => {
        this.busyConnectionId.set(null);
        this.previewConnection.set(connection);
        this.previewRange.set({ from: preview.from, to: preview.to });
        this.rows.set(preview.items.map(item => ({
          ...item,
          // New RON transactions are ticked; already imported ones and other currencies are not.
          selected: !item.alreadyImported && item.currency === 'RON',
          categoryId: item.suggestedCategoryId
        })));
      },
      error: error => {
        this.busyConnectionId.set(null);
        this.error.set(this.describe(error));
      }
    });
  }

  toggle(row: PreviewRow, selected: boolean): void {
    this.updateRow(row, { selected });
  }

  setCategory(row: PreviewRow, value: string): void {
    this.updateRow(row, { categoryId: value ? Number(value) : null });
  }

  toggleAll(selected: boolean): void {
    this.rows.update(rows => rows.map(row => (row.alreadyImported ? row : { ...row, selected })));
  }

  cancelPreview(): void {
    this.previewConnection.set(null);
    this.rows.set([]);
  }

  importSelected(): void {
    const connection = this.previewConnection();
    if (!connection || !this.canImport()) {
      return;
    }

    this.importing.set(true);
    this.clearAlerts();

    const items = this.selectedRows().map(row => ({
      externalId: row.externalId,
      date: row.date,
      amount: row.amount,
      type: row.type,
      description: row.description,
      categoryId: row.categoryId as number
    }));

    this.api.import(connection.id, items).subscribe({
      next: result => {
        this.importing.set(false);
        this.cancelPreview();
        this.message.set(this.i18n.t('Am importat {n} tranzactii.', { n: result.imported })
          + (result.skipped > 0 ? ' ' + this.i18n.t('{n} erau deja importate.', { n: result.skipped }) : ''));
        this.load();
      },
      error: error => {
        this.importing.set(false);
        this.error.set(this.describe(error));
      }
    });
  }

  disconnect(connection: BankConnection): void {
    if (!confirm(this.i18n.t('Sigur vrei sa deconectezi {bank}? Tranzactiile deja importate raman.', { bank: connection.aspspName }))) {
      return;
    }

    this.busyConnectionId.set(connection.id);
    this.api.disconnect(connection.id).subscribe({
      next: () => {
        this.busyConnectionId.set(null);
        if (this.previewConnection()?.id === connection.id) {
          this.cancelPreview();
        }
        this.message.set('Banca a fost deconectata.');
        this.load();
      },
      error: error => {
        this.busyConnectionId.set(null);
        this.error.set(this.describe(error));
      }
    });
  }

  daysLeft(connection: BankConnection): number {
    return Math.max(0, Math.ceil((new Date(connection.validUntil).getTime() - Date.now()) / 86_400_000));
  }

  formatDate(value: string | null): string {
    if (!value) {
      return '';
    }
    return new Date(value.length === 10 ? `${value}T12:00:00` : value)
      .toLocaleDateString(this.i18n.locale(), { day: 'numeric', month: 'short', year: 'numeric' });
  }

  private updateRow(target: PreviewRow, changes: Partial<PreviewRow>): void {
    this.rows.update(rows => rows.map(row => (row.externalId === target.externalId ? { ...row, ...changes } : row)));
  }

  private clearAlerts(clearMessage = true): void {
    this.error.set('');
    if (clearMessage) {
      this.message.set('');
    }
  }

  private describe(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'API-ul nu raspunde. Porneste backend-ul (dotnet run --launch-profile https).';
    }

    // Only short plain-text messages come from our own checks; a 500 returns a whole error page.
    const text = typeof error.error === 'string' && error.status === 400 ? error.error : '';

    if (error.status >= 500 && error.status !== 502) {
      return 'Eroare pe server. Ai rulat migratia BankSync (dotnet ef database update)? Uita-te in terminalul API-ului.';
    }

    if (error.status === 502) {
      return this.i18n.t('Banca nu a raspuns ({detail}). Incearca din nou peste cateva minute.', { detail: text || '502' });
    }
    if (/expired|revoked|reconnect/i.test(text)) {
      return 'Accesul la banca a expirat. Deconecteaz-o si conecteaz-o din nou.';
    }
    if (/authorization/i.test(text)) {
      return 'Autorizarea la banca a expirat sau a fost deja folosita. Incearca din nou.';
    }
    if (error.status === 404) {
      return 'Conexiunea nu mai exista.';
    }

    return text || 'Ceva n-a mers. Incearca din nou.';
  }
}
