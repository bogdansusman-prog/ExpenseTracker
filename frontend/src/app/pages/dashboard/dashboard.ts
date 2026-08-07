import {
  Component,
  computed,
  inject,
  OnInit,
  signal
} from '@angular/core';

import { ExpenseApiService } from '../../services/expense-api.service';
import { FinancialTransaction } from '../../models/financial-transaction.model';

@Component({
  selector: 'app-dashboard',
  imports: [],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class Dashboard implements OnInit {
  private readonly apiService = inject(ExpenseApiService);

  readonly transactions =
    signal<FinancialTransaction[]>([]);

  readonly loading = signal(true);
  readonly errorMessage = signal('');

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
    () =>
      this.totalIncome() -
      this.totalExpenses()
  );

  readonly transactionCount = computed(
    () => this.transactions().length
  );

  readonly incomeCount = computed(
    () =>
      this.transactions()
        .filter(
          transaction =>
            transaction.type === 1
        )
        .length
  );

  readonly expenseCount = computed(
    () =>
      this.transactions()
        .filter(
          transaction =>
            transaction.type === 2
        )
        .length
  );

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.apiService
      .getTransactions()
      .subscribe({
        next: transactions => {
          this.transactions.set(
            transactions
          );

          this.loading.set(false);
        },

        error: error => {
          console.error(
            'Dashboard error:',
            error
          );

          this.errorMessage.set(
            'Datele nu au putut fi incarcate.'
          );

          this.loading.set(false);
        }
      });
  }

  formatMoney(value: number): string {
    return value.toLocaleString(
      'ro-RO',
      {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
      }
    );
  }
}