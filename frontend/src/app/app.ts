import {
  Component,
  computed,
  inject,
  OnInit,
  signal
} from '@angular/core';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { forkJoin } from 'rxjs';

import { Category } from './models/category.model';
import {
  FinancialTransaction,
  FinancialTransactionRequest,
  TransactionType
} from './models/financial-transaction.model';
import { ExpenseApiService } from './services/expense-api.service';

@Component({
  selector: 'app-root',
  imports: [ReactiveFormsModule],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  private readonly apiService = inject(ExpenseApiService);
  private readonly formBuilder = inject(FormBuilder);

  readonly categories = signal<Category[]>([]);
  readonly transactions = signal<FinancialTransaction[]>([]);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly errorMessage = signal('');
  readonly successMessage = signal('');

  readonly totalIncome = computed(() =>
    this.transactions()
      .filter(transaction => transaction.type === 1)
      .reduce((total, transaction) => total + transaction.amount, 0)
  );

  readonly totalExpenses = computed(() =>
    this.transactions()
      .filter(transaction => transaction.type === 2)
      .reduce((total, transaction) => total + transaction.amount, 0)
  );

  readonly balance = computed(
    () => this.totalIncome() - this.totalExpenses()
  );

  readonly transactionForm = this.formBuilder.nonNullable.group({
    title: [
      '',
      [
        Validators.required,
        Validators.maxLength(150)
      ]
    ],
    amount: [
      0,
      [
        Validators.required,
        Validators.min(0.01)
      ]
    ],
    date: [
      new Date().toISOString().slice(0, 10),
      Validators.required
    ],
    type: [
      2 as TransactionType,
      Validators.required
    ],
    categoryId: [
      0,
      [
        Validators.required,
        Validators.min(1)
      ]
    ],
    description: [
      '',
      Validators.maxLength(500)
    ]
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    forkJoin({
      categories: this.apiService.getCategories(),
      transactions: this.apiService.getTransactions()
    }).subscribe({
      next: result => {
        this.categories.set(result.categories);
        this.transactions.set(result.transactions);

        if (
          result.categories.length > 0 &&
          this.transactionForm.controls.categoryId.value === 0
        ) {
          this.transactionForm.patchValue({
            categoryId: result.categories[0].id
          });
        }

        this.loading.set(false);
      },
      error: error => {
        console.error('API error:', error);

        this.errorMessage.set(
          'Nu s-au putut încărca datele din backend.'
        );

        this.loading.set(false);
      }
    });
  }

  createTransaction(): void {
    this.successMessage.set('');
    this.errorMessage.set('');

    if (this.transactionForm.invalid) {
      this.transactionForm.markAllAsTouched();
      return;
    }

    const formValue = this.transactionForm.getRawValue();

    const request: FinancialTransactionRequest = {
      title: formValue.title.trim(),
      amount: Number(formValue.amount),
      date: new Date(
        `${formValue.date}T12:00:00`
      ).toISOString(),
      type: Number(formValue.type) as TransactionType,
      categoryId: Number(formValue.categoryId),
      description: formValue.description.trim() || null
    };

    this.saving.set(true);

    this.apiService.createTransaction(request).subscribe({
      next: createdTransaction => {
        this.transactions.update(currentTransactions => [
          createdTransaction,
          ...currentTransactions
        ]);

        this.transactionForm.reset({
          title: '',
          amount: 0,
          date: new Date().toISOString().slice(0, 10),
          type: 2,
          categoryId: this.categories()[0]?.id ?? 0,
          description: ''
        });

        this.successMessage.set(
          'Tranzacția a fost adăugată.'
        );

        this.saving.set(false);
      },
      error: error => {
        console.error('Create transaction error:', error);

        this.errorMessage.set(
          'Tranzacția nu a putut fi adăugată.'
        );

        this.saving.set(false);
      }
    });
  }

  deleteTransaction(id: number): void {
    const confirmed = window.confirm(
      'Sigur vrei să ștergi această tranzacție?'
    );

    if (!confirmed) {
      return;
    }

    this.errorMessage.set('');
    this.successMessage.set('');

    this.apiService.deleteTransaction(id).subscribe({
      next: () => {
        this.transactions.update(currentTransactions =>
          currentTransactions.filter(
            transaction => transaction.id !== id
          )
        );

        this.successMessage.set(
          'Tranzacția a fost ștearsă.'
        );
      },
      error: error => {
        console.error('Delete transaction error:', error);

        this.errorMessage.set(
          'Tranzacția nu a putut fi ștearsă.'
        );
      }
    });
  }

  formatMoney(value: number): string {
    return value.toLocaleString('ro-RO', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });
  }
}