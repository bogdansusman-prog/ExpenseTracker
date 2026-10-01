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
} from '../../models/category.model';

import {
  FinancialTransaction
} from '../../models/financial-transaction.model';

import {
  ExpenseApiService
} from '../../services/expense-api.service';
import { I18n } from '../../i18n/i18n.service';
import { TranslatePipe } from '../../i18n/translate.pipe';

@Component({
  selector: 'app-categories',
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './categories.html',
  styleUrl: './categories.scss'
})
export class Categories implements OnInit {
  private readonly i18n = inject(I18n);


  private readonly apiService =
    inject(ExpenseApiService);

  private readonly formBuilder =
    inject(FormBuilder);

  readonly categories =
    signal<Category[]>([]);

  readonly transactions =
    signal<FinancialTransaction[]>([]);

  readonly loading =
    signal(true);

  readonly saving =
    signal(false);

  readonly editingCategoryId =
    signal<number | null>(null);

  readonly errorMessage =
    signal('');

  readonly successMessage =
    signal('');

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

  readonly totalCategories =
    computed(
      () => this.categories().length
    );

  readonly usedCategories =
    computed(() => {

      const usedIds =
        new Set(
          this.transactions().map(
            transaction =>
              transaction.categoryId
          )
        );

      return this.categories()
        .filter(
          category =>
            usedIds.has(category.id)
        )
        .length;
    });

  readonly unusedCategories =
    computed(
      () =>
        this.totalCategories() -
        this.usedCategories()
    );

  ngOnInit(): void {
    this.loadData();
  }

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

        this.loading.set(false);
      },

      error: error => {

        console.error(
          'Categories load error:',
          error
        );

        this.errorMessage.set(
          'Datele nu au putut fi incarcate.'
        );

        this.loading.set(false);
      }

    });
  }

  saveCategory(): void {

    this.clearMessages();

    if (
      this.categoryForm.invalid
    ) {

      this.categoryForm
        .markAllAsTouched();

      return;
    }

    const name =
      this.categoryForm.controls
        .name.value.trim();

    if (!name) {

      this.categoryForm.controls
        .name.setErrors({
          required: true
        });

      return;
    }

    const request:
      CategoryRequest = {
        name
      };

    const categoryId =
      this.editingCategoryId();

    this.saving.set(true);

    if (
      categoryId === null
    ) {

      this.createCategory(
        request
      );

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

          this.categories.update(
            categories =>
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

          this.resetForm();

          this.successMessage.set(
            'Categoria a fost adaugata.'
          );

          this.saving.set(false);
        },

        error: error => {

          this.handleCategoryError(
            error,
            'Categoria nu a putut fi adaugata.'
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

          this.categories.update(
            categories =>
              categories
                .map(
                  category =>
                    category.id ===
                    categoryId
                      ? {
                          ...category,
                          name:
                            request.name
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

          this.transactions.update(
            transactions =>
              transactions.map(
                transaction =>
                  transaction.categoryId ===
                  categoryId
                    ? {
                        ...transaction,
                        categoryName:
                          request.name
                      }
                    : transaction
              )
          );

          this.resetForm();

          this.successMessage.set(
            'Categoria a fost modificata.'
          );

          this.saving.set(false);
        },

        error: error => {

          this.handleCategoryError(
            error,
            'Categoria nu a putut fi modificata.'
          );
        }

      });
  }

  startEdit(
    category: Category
  ): void {

    this.clearMessages();

    this.editingCategoryId.set(
      category.id
    );

    this.categoryForm.setValue({
      name: category.name
    });

    setTimeout(() => {
      document
        .getElementById(
          'category-name'
        )
        ?.focus();
    });
  }

  cancelEdit(): void {
    this.resetForm();
  }

  deleteCategory(
    category: Category
  ): void {

    this.clearMessages();

    const count =
      this.getTransactionCount(
        category.id
      );

    if (count > 0) {

      this.errorMessage.set(
        'Categoria nu poate fi stearsa deoarece are tranzactii asociate.'
      );

      return;
    }

    const confirmed =
      window.confirm(
        this.i18n.t('Sigur vrei sa stergi categoria "{name}"?', { name: category.name })
      );

    if (!confirmed) {
      return;
    }

    this.apiService
      .deleteCategory(
        category.id
      )
      .subscribe({

        next: () => {

          this.categories.update(
            categories =>
              categories.filter(
                existingCategory =>
                  existingCategory.id !==
                  category.id
              )
          );

          if (
            this.editingCategoryId() ===
            category.id
          ) {
            this.resetForm();
          }

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

          if (
            error.status === 409
          ) {

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

  getTransactionCount(
    categoryId: number
  ): number {

    return this.transactions()
      .filter(
        transaction =>
          transaction.categoryId ===
          categoryId
      )
      .length;
  }

  private resetForm(): void {

    this.editingCategoryId.set(
      null
    );

    this.categoryForm.reset({
      name: ''
    });
  }

  private handleCategoryError(
    error: HttpErrorResponse,
    fallbackMessage: string
  ): void {

    console.error(
      'Category error:',
      error
    );

    if (
      error.status === 409
    ) {

      this.errorMessage.set(
        'Exista deja o categorie cu acest nume.'
      );

    } else {

      this.errorMessage.set(
        fallbackMessage
      );
    }

    this.saving.set(false);
  }

  private clearMessages(): void {

    this.errorMessage.set('');

    this.successMessage.set('');
  }
}