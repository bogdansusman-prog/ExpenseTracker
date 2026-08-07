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

import { Category } from '../../models/category.model';

import {
  FinancialTransaction,
  FinancialTransactionRequest,
  TransactionType
} from '../../models/financial-transaction.model';

import { ExpenseApiService } from '../../services/expense-api.service';

@Component({
  selector: 'app-transactions',
  imports: [ReactiveFormsModule],
  templateUrl: './transactions.html',
  styleUrl: './transactions.scss'
})
export class Transactions implements OnInit {

  private readonly apiService =
    inject(ExpenseApiService);

  private readonly formBuilder =
    inject(FormBuilder);

  // DATA

  readonly categories =
    signal<Category[]>([]);

  readonly transactions =
    signal<FinancialTransaction[]>([]);

  // STATE

  readonly loading =
    signal(true);

  readonly saving =
    signal(false);

  readonly errorMessage =
    signal('');

  readonly successMessage =
    signal('');

  // FILTERS

  readonly filterCategoryId =
    signal(0);

  readonly filterType =
    signal(0);

  readonly filterDateFrom =
    signal('');

  readonly filterDateTo =
    signal('');

  // SELECTION

  readonly selectedTransactionIds =
    signal<Set<number>>(
      new Set<number>()
    );

  // FORM

  readonly transactionForm =
    this.formBuilder.nonNullable.group({

      categoryId: [
        0,
        [
          Validators.required,
          Validators.min(1)
        ]
      ],

      amount: [
        0,
        [
          Validators.required,
          Validators.min(0.01)
        ]
      ],

      type: [
        2 as TransactionType,
        Validators.required
      ],

      date: [
        new Date()
          .toISOString()
          .slice(0, 10),

        Validators.required
      ],

      description: [
        '',
        Validators.maxLength(500)
      ]

    });

  // ONLY USED CATEGORIES

  readonly transactionCategories =
    computed(() => {

      const usedCategoryIds =
        new Set(
          this.transactions().map(
            transaction =>
              transaction.categoryId
          )
        );

      return this.categories().filter(
        category =>
          usedCategoryIds.has(
            category.id
          )
      );
    });

  // FILTERED TRANSACTIONS

  readonly filteredTransactions =
    computed(() => {

      const categoryId =
        this.filterCategoryId();

      const type =
        this.filterType();

      const dateFrom =
        this.filterDateFrom();

      const dateTo =
        this.filterDateTo();

      return this.transactions().filter(
        transaction => {

          const transactionDate =
            transaction.date.slice(0, 10);

          const matchesCategory =
            categoryId === 0 ||
            transaction.categoryId ===
            categoryId;

          const matchesType =
            type === 0 ||
            transaction.type === type;

          const matchesDateFrom =
            !dateFrom ||
            transactionDate >=
            dateFrom;

          const matchesDateTo =
            !dateTo ||
            transactionDate <=
            dateTo;

          return (
            matchesCategory &&
            matchesType &&
            matchesDateFrom &&
            matchesDateTo
          );
        }
      );
    });

  // SELECTION

  readonly selectedCount =
    computed(
      () =>
        this.selectedTransactionIds()
          .size
    );

  readonly selectedVisibleCount =
    computed(() => {

      const selectedIds =
        this.selectedTransactionIds();

      return this
        .filteredTransactions()
        .filter(
          transaction =>
            selectedIds.has(
              transaction.id
            )
        )
        .length;
    });

  readonly allVisibleTransactionsSelected =
    computed(() => {

      const visibleTransactions =
        this.filteredTransactions();

      if (
        visibleTransactions.length === 0
      ) {
        return false;
      }

      const selectedIds =
        this.selectedTransactionIds();

      return visibleTransactions.every(
        transaction =>
          selectedIds.has(
            transaction.id
          )
      );
    });

  ngOnInit(): void {
    this.loadData();
  }

  // LOAD

  loadData(): void {

    this.loading.set(true);

    this.errorMessage.set('');

    forkJoin({

      categories:
        this.apiService
          .getCategories(),

      transactions:
        this.apiService
          .getTransactions()

    }).subscribe({

      next: result => {

        this.categories.set(
          result.categories
        );

        this.transactions.set(
          result.transactions
        );

        if (
          result.categories.length > 0
        ) {
          this.transactionForm
            .patchValue({
              categoryId:
                result.categories[0].id
            });
        }

        this.clearTransactionSelection();

        this.ensureValidCategoryFilter();

        this.loading.set(false);
      },

      error: error => {

        console.error(
          'Transactions load error:',
          error
        );

        this.errorMessage.set(
          'Datele nu au putut fi incarcate.'
        );

        this.loading.set(false);
      }

    });
  }

  // CREATE

  createTransaction(): void {

    this.clearMessages();

    if (
      this.transactionForm.invalid
    ) {

      this.transactionForm
        .markAllAsTouched();

      return;
    }

    const formValue =
      this.transactionForm
        .getRawValue();

    const request:
      FinancialTransactionRequest = {

        categoryId:
          Number(
            formValue.categoryId
          ),

        amount:
          Number(
            formValue.amount
          ),

        type:
          Number(
            formValue.type
          ) as TransactionType,

        date:
          new Date(
            `${formValue.date}T12:00:00`
          ).toISOString(),

        description:
          formValue.description
            .trim() || null

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

            categoryId:
              this.categories()[0]?.id ??
              0,

            amount: 0,

            type: 2,

            date:
              new Date()
                .toISOString()
                .slice(0, 10),

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

  // DELETE ONE

  deleteTransaction(
    id: number
  ): void {

    this.clearMessages();

    const confirmed =
      window.confirm(
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

          const selectedIds =
            new Set(
              this.selectedTransactionIds()
            );

          selectedIds.delete(id);

          this.selectedTransactionIds
            .set(selectedIds);

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

  // SELECTION

  toggleTransactionSelection(
    transactionId: number
  ): void {

    const selectedIds =
      new Set(
        this.selectedTransactionIds()
      );

    if (
      selectedIds.has(
        transactionId
      )
    ) {

      selectedIds.delete(
        transactionId
      );

    } else {

      selectedIds.add(
        transactionId
      );
    }

    this.selectedTransactionIds
      .set(selectedIds);
  }

  toggleSelectAllVisible(): void {

    const selectedIds =
      new Set(
        this.selectedTransactionIds()
      );

    const visibleTransactions =
      this.filteredTransactions();

    if (
      this.allVisibleTransactionsSelected()
    ) {

      visibleTransactions.forEach(
        transaction =>
          selectedIds.delete(
            transaction.id
          )
      );

    } else {

      visibleTransactions.forEach(
        transaction =>
          selectedIds.add(
            transaction.id
          )
      );
    }

    this.selectedTransactionIds
      .set(selectedIds);
  }

  clearTransactionSelection(): void {

    this.selectedTransactionIds.set(
      new Set<number>()
    );
  }

  // BULK DELETE

  deleteSelectedTransactions(): void {

    const selectedIds = [
      ...this.selectedTransactionIds()
    ];

    if (
      selectedIds.length === 0
    ) {
      return;
    }

    const confirmed =
      window.confirm(
        `Sigur vrei sa stergi ${selectedIds.length} tranzactii?`
      );

    if (!confirmed) {
      return;
    }

    this.clearMessages();

    const requests =
      selectedIds.map(
        id =>
          this.apiService
            .deleteTransaction(id)
      );

    forkJoin(requests)
      .subscribe({

        next: () => {

          const selectedIdSet =
            new Set(
              selectedIds
            );

          this.transactions.update(
            transactions =>
              transactions.filter(
                transaction =>
                  !selectedIdSet.has(
                    transaction.id
                  )
              )
          );

          this.clearTransactionSelection();

          this.ensureValidCategoryFilter();

          this.successMessage.set(
            `${selectedIds.length} tranzactii au fost sterse.`
          );
        },

        error: error => {

          console.error(
            'Bulk delete error:',
            error
          );

          this.errorMessage.set(
            'Tranzactiile selectate nu au putut fi sterse.'
          );

          // Some requests may have succeeded.
          // Reload to synchronize with database.
          this.loadData();
        }

      });
  }

  // FILTERS

  setCategoryFilter(
    event: Event
  ): void {

    const value =
      Number(
        (
          event.target as
            HTMLSelectElement
        ).value
      );

    this.filterCategoryId.set(
      value
    );

    this.clearTransactionSelection();
  }

  setTypeFilter(
    event: Event
  ): void {

    const value =
      Number(
        (
          event.target as
            HTMLSelectElement
        ).value
      );

    this.filterType.set(
      value
    );

    this.clearTransactionSelection();
  }

  setDateFromFilter(
    event: Event
  ): void {

    const value =
      (
        event.target as
          HTMLInputElement
      ).value;

    this.filterDateFrom.set(
      value
    );

    this.clearTransactionSelection();
  }

  setDateToFilter(
    event: Event
  ): void {

    const value =
      (
        event.target as
          HTMLInputElement
      ).value;

    this.filterDateTo.set(
      value
    );

    this.clearTransactionSelection();
  }

  resetTransactionFilters(): void {

    this.filterCategoryId.set(0);

    this.filterType.set(0);

    this.filterDateFrom.set('');

    this.filterDateTo.set('');

    this.clearTransactionSelection();
  }

  private ensureValidCategoryFilter(): void {

    if (
      this.filterCategoryId() === 0
    ) {
      return;
    }

    const stillExists =
      this.transactionCategories()
        .some(
          category =>
            category.id ===
            this.filterCategoryId()
        );

    if (!stillExists) {

      this.filterCategoryId.set(
        0
      );
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
}