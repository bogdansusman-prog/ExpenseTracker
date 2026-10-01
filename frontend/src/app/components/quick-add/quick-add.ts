import { Component, inject, OnInit, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { Category } from '../../models/category.model';
import { TransactionType } from '../../models/financial-transaction.model';
import { QuickAddResult } from '../../models/insights.model';
import { ExpenseApiService } from '../../services/expense-api.service';
import { InsightsApiService } from '../../services/insights-api.service';
import { SettingsStore } from '../../services/settings.store';
import { formatHours, formatMoney } from '../../shared/format';

interface Draft {
  amount: number | null;
  type: TransactionType;
  date: string;
  categoryId: number | null;
  description: string;
}

/**
 * "ieri 45 lei pizza cu Andrei" -> transaction draft -> confirm -> saved.
 */
@Component({
  selector: 'app-quick-add',
  imports: [FormsModule],
  templateUrl: './quick-add.html',
  styleUrl: './quick-add.scss'
})
export class QuickAdd implements OnInit {
  private readonly insightsApi = inject(InsightsApiService);
  private readonly expenseApi = inject(ExpenseApiService);
  readonly settings = inject(SettingsStore);

  readonly saved = output<void>();

  readonly text = signal('');
  readonly parsing = signal(false);
  readonly saving = signal(false);
  readonly errorMessage = signal('');
  readonly successMessage = signal('');
  readonly result = signal<QuickAddResult | null>(null);
  readonly categories = signal<Category[]>([]);

  draft: Draft = QuickAdd.emptyDraft();
  readonly showDraft = signal(false);

  private static emptyDraft(): Draft {
    return { amount: null, type: 2, date: '', categoryId: null, description: '' };
  }

  readonly examples = [
    'ieri 45 lei pizza cu Andrei',
    'salariu 4500 lei',
    '23,50 uber vineri',
    'factura curent 210 lei 15.09'
  ];

  readonly formatMoney = formatMoney;
  readonly formatHours = formatHours;

  ngOnInit(): void {
    this.settings.load();
    this.expenseApi.getCategories().subscribe({
      next: categories => this.categories.set(categories)
    });
  }

  useExample(example: string): void {
    this.text.set(example);
    this.parse();
  }

  parse(): void {
    const text = this.text().trim();

    if (!text) {
      return;
    }

    this.parsing.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.insightsApi.parseQuickAdd(text).subscribe({
      next: result => {
        this.result.set(result);
        this.draft = {
          amount: result.draft.amount,
          type: result.draft.type,
          date: result.draft.date,
          categoryId: result.draft.categoryId,
          description: result.draft.description ?? ''
        };
        this.showDraft.set(true);
        this.parsing.set(false);
      },
      error: () => {
        this.errorMessage.set('Textul nu a putut fi analizat. Verifica daca API-ul ruleaza.');
        this.parsing.set(false);
      }
    });
  }

  draftHours(): string {
    return this.draft.amount ? formatHours(this.settings.workHours(Number(this.draft.amount))) : '';
  }

  canSave(): boolean {
    return this.showDraft() && !!this.draft.amount && Number(this.draft.amount) > 0 && !!this.draft.categoryId && !!this.draft.date;
  }

  save(): void {
    if (!this.canSave()) {
      return;
    }

    this.saving.set(true);

    this.expenseApi.createTransaction({
      amount: Number(this.draft.amount),
      type: Number(this.draft.type) as TransactionType,
      date: `${this.draft.date}T12:00:00`,
      categoryId: Number(this.draft.categoryId),
      description: this.draft.description.trim() || null
    }).subscribe({
      next: () => {
        this.successMessage.set('Tranzactia a fost adaugata.');
        this.saving.set(false);
        this.cancel();
        this.saved.emit();
      },
      error: () => {
        this.errorMessage.set('Tranzactia nu a putut fi salvata.');
        this.saving.set(false);
      }
    });
  }

  cancel(): void {
    this.draft = QuickAdd.emptyDraft();
    this.showDraft.set(false);
    this.result.set(null);
    this.text.set('');
  }
}
