import { HttpErrorResponse } from '@angular/common/http';
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

import {
  Category,
  CategoryRequest
} from './models/category.model';

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
  readonly categorySaving = signal(false);

  readonly editingCategoryId = signal<number | null>(null);

  readonly errorMessage = signal('');
  readonly successMessage = signal('');

  // FILTERS

  readonly filterCategoryId = signal(0);
  readonly filterType = signal(0);
  readonly filterDateFrom = signal('');
  readonly filterDateTo = signal('');

  // DASHBOARD

  readonly totalIncome = computed(() =>
    this.transactions()
      .filter(transaction => transaction.type === 1)
      .reduce(
        (total, transaction) =>
          total + transaction.amount,
        0
      )
  );

  readonly totalExpenses = computed(() =>
    this.transactions()
      .filter(transaction => transaction.type === 2)
      .reduce(
        (total, transaction) =>
          total + transaction.amount,
        0
      )
  );

  readonly balance = computed(
    () => this.totalIncome() - this.totalExpenses()
  );

  // CATEGORIES THAT ACTUALLY HAVE TRANSACTIONS

  readonly transactionCategories = computed(() => {
    const usedCategoryIds = new Set(
      this.transactions().map(
        transaction => transaction.categoryId
      )
    );

    return this.categories().filter(
      category =>
        usedCategoryIds.has(category.id)
    );
  });

  // FILTERED TRANSACTIONS

  readonly filteredTransactions = computed(() => {
    const categoryId = this.filterCategoryId();
    const type = this.filterType();
    const dateFrom = this.filterDateFrom();
    const dateTo = this.filterDateTo();

    return this.transactions().filter(transaction => {
      const transactionDate =
        transaction.date.slice(0, 10);

      const matchesCategory =
        categoryId === 0 ||
        transaction.categoryId === categoryId;

      const matchesType =
        type === 0 ||
        transaction.type === type;

      const matchesDateFrom =
        !dateFrom ||
        transactionDate >= dateFrom;

      const matchesDateTo =
        !dateTo ||
        transactionDate <= dateTo;

      return (
        matchesCategory &&
        matchesType &&
        matchesDateFrom &&
        matchesDateTo
      );
    });
  });

  // CATEGORY FORM

  readonly categoryForm =
    this.formBuilder.nonNullable.group({
      name: [
        '',
        [
          Validators.required,
          Validators.maxLength(100)
        ]
      ]
    });

  // TRANSACTION FORM

  readonly transactionForm =
    this.formBuilder.nonNullable.group({
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

  // LOAD DATA

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
          this.transactionForm.controls
            .categoryId.value === 0
        ) {
          this.transactionForm.patchValue({
            categoryId: result.categories[0].id
          });
        }

        this.ensureValidCategoryFilter();

        this.loading.set(false);
      },

      error: error => {
        console.error('API error:', error);

        this.errorMessage.set(
          'Nu s-au putut incarca datele din backend.'
        );

        this.loading.set(false);
      }
    });
  }

  // CATEGORY MANAGEMENT

  saveCategory(): void {
    this.clearMessages();

    if (this.categoryForm.invalid) {
      this.categoryForm.markAllAsTouched();
      return;
    }

    const name =
      this.categoryForm.controls.name.value.trim();

    if (!name) {
      this.categoryForm.controls.name.setErrors({
        required: true
      });

      return;
    }

    const request: CategoryRequest = {
      name
    };

    const categoryId =
      this.editingCategoryId();

    this.categorySaving.set(true);

    if (categoryId === null) {
      this.createCategory(request);
      return;
    }

    this.updateCategory(
      categoryId,
      request
    );
  }

  private createCategory(
    request: CategoryRequest
  ): void {
    this.apiService
      .createCategory(request)
      .subscribe({
        next: createdCategory => {
          this.categories.update(categories =>
            [
              ...categories,
              createdCategory
            ].sort(
              (first, second) =>
                first.name.localeCompare(
                  second.name,
                  'ro'
                )
            )
          );

          if (
            this.transactionForm.controls
              .categoryId.value === 0
          ) {
            this.transactionForm.patchValue({
              categoryId:
                createdCategory.id
            });
          }

          this.categoryForm.reset({
            name: ''
          });

          this.successMessage.set(
            'Categoria a fost adaugata.'
          );

          this.categorySaving.set(false);
        },

        error: error => {
          this.handleCategoryError(
            error,
            'Categoria nu a putut fi adaugata.',
            'Exista deja o categorie cu acest nume.'
          );
        }
      });
  }

  private updateCategory(
    categoryId: number,
    request: CategoryRequest
  ): void {
    this.apiService
      .updateCategory(
        categoryId,
        request
      )
      .subscribe({
        next: () => {
          this.categories.update(categories =>
            categories
              .map(category =>
                category.id === categoryId
                  ? {
                      ...category,
                      name: request.name
                    }
                  : category
              )
              .sort(
                (first, second) =>
                  first.name.localeCompare(
                    second.name,
                    'ro'
                  )
              )
          );

          // Update category name in transaction list
          this.transactions.update(transactions =>
            transactions.map(transaction =>
              transaction.categoryId === categoryId
                ? {
                    ...transaction,
                    categoryName: request.name
                  }
                : transaction
            )
          );

          this.cancelCategoryEdit();

          this.successMessage.set(
            'Categoria a fost modificata.'
          );

          this.categorySaving.set(false);
        },

        error: error => {
          this.handleCategoryError(
            error,
            'Categoria nu a putut fi modificata.',
            'Exista deja o categorie cu acest nume.'
          );
        }
      });
  }

  startCategoryEdit(
    category: Category
  ): void {
    this.clearMessages();

    this.editingCategoryId.set(
      category.id
    );

    this.categoryForm.setValue({
      name: category.name
    });
  }

  cancelCategoryEdit(): void {
    this.editingCategoryId.set(null);

    this.categoryForm.reset({
      name: ''
    });
  }

  deleteCategory(
    category: Category
  ): void {
    this.clearMessages();

    const confirmed = window.confirm(
      `Sigur vrei sa stergi categoria "${category.name}"?`
    );

    if (!confirmed) {
      return;
    }

    this.apiService
      .deleteCategory(category.id)
      .subscribe({
        next: () => {
          const remainingCategories =
            this.categories().filter(
              existingCategory =>
                existingCategory.id !==
                category.id
            );

          this.categories.set(
            remainingCategories
          );

          if (
            this.transactionForm.controls
              .categoryId.value ===
            category.id
          ) {
            this.transactionForm.patchValue({
              categoryId:
                remainingCategories[0]?.id ??
                0
            });
          }

          if (
            this.editingCategoryId() ===
            category.id
          ) {
            this.cancelCategoryEdit();
          }

          this.ensureValidCategoryFilter();

          this.successMessage.set(
            'Categoria a fost stearsa.'
          );
        },

        error: (
          error: HttpErrorResponse
        ) => {
          console.error(
            'Delete category error:',
            error
          );

          if (error.status === 409) {
            this.errorMessage.set(
              'Categoria nu poate fi stearsa deoarece are tranzactii asociate.'
            );
          } else {
            this.errorMessage.set(
              'Categoria nu a putut fi stearsa.'
            );
          }
        }
      });
  }

  // TRANSACTION MANAGEMENT

  createTransaction(): void {
    this.clearMessages();

    if (this.transactionForm.invalid) {
      this.transactionForm.markAllAsTouched();
      return;
    }

    const formValue =
      this.transactionForm.getRawValue();

    const request:
      FinancialTransactionRequest = {
        amount:
          Number(formValue.amount),

        date: new Date(
          `${formValue.date}T12:00:00`
        ).toISOString(),

        type:
          Number(
            formValue.type
          ) as TransactionType,

        categoryId:
          Number(
            formValue.categoryId
          ),

        description:
          formValue.description.trim() ||
          null
      };

    this.saving.set(true);

    this.apiService
      .createTransaction(request)
      .subscribe({
        next: createdTransaction => {
          this.transactions.update(
            transactions => [
              createdTransaction,
              ...transactions
            ]
          );

          this.transactionForm.reset({
            amount: 0,

            date: new Date()
              .toISOString()
              .slice(0, 10),

            type: 2,

            categoryId:
              this.categories()[0]?.id ??
              0,

            description: ''
          });

          this.successMessage.set(
            'Tranzactia a fost adaugata.'
          );

          this.saving.set(false);
        },

        error: error => {
          console.error(
            'Create transaction error:',
            error
          );

          this.errorMessage.set(
            'Tranzactia nu a putut fi adaugata.'
          );

          this.saving.set(false);
        }
      });
  }

  deleteTransaction(
    id: number
  ): void {
    this.clearMessages();

    const confirmed = window.confirm(
      'Sigur vrei sa stergi aceasta tranzactie?'
    );

    if (!confirmed) {
      return;
    }

    this.apiService
      .deleteTransaction(id)
      .subscribe({
        next: () => {
          this.transactions.update(
            transactions =>
              transactions.filter(
                transaction =>
                  transaction.id !== id
              )
          );

          // If the deleted transaction was the last one
          // from the selected category, reset the filter.
          this.ensureValidCategoryFilter();

          this.successMessage.set(
            'Tranzactia a fost stearsa.'
          );
        },

        error: error => {
          console.error(
            'Delete transaction error:',
            error
          );

          this.errorMessage.set(
            'Tranzactia nu a putut fi stearsa.'
          );
        }
      });
  }

  // FILTERS

  setCategoryFilter(
    event: Event
  ): void {
    const value = Number(
      (
        event.target as HTMLSelectElement
      ).value
    );

    this.filterCategoryId.set(value);
  }

  setTypeFilter(
    event: Event
  ): void {
    const value = Number(
      (
        event.target as HTMLSelectElement
      ).value
    );

    this.filterType.set(value);
  }

  setDateFromFilter(
    event: Event
  ): void {
    const value =
      (
        event.target as HTMLInputElement
      ).value;

    this.filterDateFrom.set(value);
  }

  setDateToFilter(
    event: Event
  ): void {
    const value =
      (
        event.target as HTMLInputElement
      ).value;

    this.filterDateTo.set(value);
  }

  resetTransactionFilters(): void {
    this.filterCategoryId.set(0);
    this.filterType.set(0);
    this.filterDateFrom.set('');
    this.filterDateTo.set('');
  }

  private ensureValidCategoryFilter(): void {
    if (
      this.filterCategoryId() === 0
    ) {
      return;
    }

    const selectedCategoryStillExists =
      this.transactionCategories().some(
        category =>
          category.id ===
          this.filterCategoryId()
      );

    if (
      !selectedCategoryStillExists
    ) {
      this.filterCategoryId.set(0);
    }
  }

  // HELPERS

  formatMoney(
    value: number
  ): string {
    return value.toLocaleString(
      'ro-RO',
      {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
      }
    );
  }

  private clearMessages(): void {
    this.errorMessage.set('');
    this.successMessage.set('');
  }

  private handleCategoryError(
    error: HttpErrorResponse,
    fallbackMessage: string,
    conflictMessage: string
  ): void {
    console.error(
      'Category error:',
      error
    );

    this.errorMessage.set(
      error.status === 409
        ? conflictMessage
        : fallbackMessage
    );

    this.categorySaving.set(false);
  }
}