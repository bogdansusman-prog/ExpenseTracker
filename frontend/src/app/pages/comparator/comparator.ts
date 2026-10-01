import {
  AfterViewInit,
  Component,
  ElementRef,
  inject,
  OnDestroy,
  signal,
  ViewChild
} from '@angular/core';

import Chart from 'chart.js/auto';

import {
  FinancialTransaction
} from '../../models/financial-transaction.model';

import {
  ExpenseApiService
} from '../../services/expense-api.service';
import { I18n } from '../../i18n/i18n.service';
import { TranslatePipe } from '../../i18n/translate.pipe';

@Component({
  selector: 'app-comparator',
  imports: [TranslatePipe],
  templateUrl: './comparator.html',
  styleUrl: './comparator.scss'
})
export class Comparator
  implements AfterViewInit, OnDestroy {
  private readonly i18n = inject(I18n);


  private readonly apiService =
    inject(ExpenseApiService);

  @ViewChild('monthlyChart')
  private monthlyChartCanvas?: ElementRef<HTMLCanvasElement>;

  @ViewChild('categoryChart')
  private categoryChartCanvas?: ElementRef<HTMLCanvasElement>;

  private monthlyChart?: Chart;
  private categoryChart?: Chart;

  readonly transactions =
    signal<FinancialTransaction[]>([]);

  readonly loading =
    signal(true);

  readonly errorMessage =
    signal('');

  readonly totalIncome =
    signal(0);

  readonly totalExpenses =
    signal(0);

  readonly difference =
    signal(0);

  ngAfterViewInit(): void {
    this.loadData();
  }

  ngOnDestroy(): void {
    this.monthlyChart?.destroy();
    this.categoryChart?.destroy();
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

          this.calculateTotals(
            transactions
          );

          this.loading.set(false);

          setTimeout(() => {
            this.renderCharts();
          });
        },

        error: error => {
          console.error(
            'Comparator error:',
            error
          );

          this.errorMessage.set(
            'Datele nu au putut fi incarcate.'
          );

          this.loading.set(false);
        }
      });
  }

  private calculateTotals(
    transactions: FinancialTransaction[]
  ): void {

    const income =
      transactions
        .filter(
          transaction =>
            transaction.type === 1
        )
        .reduce(
          (total, transaction) =>
            total + transaction.amount,
          0
        );

    const expenses =
      transactions
        .filter(
          transaction =>
            transaction.type === 2
        )
        .reduce(
          (total, transaction) =>
            total + transaction.amount,
          0
        );

    this.totalIncome.set(income);
    this.totalExpenses.set(expenses);

    this.difference.set(
      income - expenses
    );
  }

  private renderCharts(): void {
    this.renderMonthlyChart();
    this.renderCategoryChart();
  }

  private renderMonthlyChart(): void {

    if (
      !this.monthlyChartCanvas
    ) {
      return;
    }

    this.monthlyChart?.destroy();

    const monthlyData =
      new Map<
        string,
        {
          income: number;
          expenses: number;
        }
      >();

    for (
      const transaction
      of this.transactions()
    ) {

      const month =
        transaction.date
          .slice(0, 7);

      if (
        !monthlyData.has(month)
      ) {
        monthlyData.set(
          month,
          {
            income: 0,
            expenses: 0
          }
        );
      }

      const data =
        monthlyData.get(month)!;

      if (
        transaction.type === 1
      ) {
        data.income +=
          transaction.amount;
      } else {
        data.expenses +=
          transaction.amount;
      }
    }

    const months =
      [...monthlyData.keys()]
        .sort();

    const incomeData =
      months.map(
        month =>
          monthlyData
            .get(month)!
            .income
      );

    const expenseData =
      months.map(
        month =>
          monthlyData
            .get(month)!
            .expenses
      );

    this.monthlyChart =
      new Chart(
        this.monthlyChartCanvas
          .nativeElement,
        {
          type: 'bar',

          data: {
            labels: months,

            datasets: [
              {
                label: this.i18n.t('Venituri'),
                data: incomeData
              },
              {
                label: this.i18n.t('Cheltuieli'),
                data: expenseData
              }
            ]
          },

          options: {
            responsive: true,
            maintainAspectRatio: false,

            plugins: {
              legend: {
                labels: {
                  color: '#d9dee7'
                }
              }
            },

            scales: {
              x: {
                ticks: {
                  color: '#b8c0cc'
                },

                grid: {
                  color:
                    'rgba(255,255,255,0.05)'
                }
              },

              y: {
                beginAtZero: true,

                ticks: {
                  color: '#b8c0cc'
                },

                grid: {
                  color:
                    'rgba(255,255,255,0.05)'
                }
              }
            }
          }
        }
      );
  }

  private renderCategoryChart(): void {

    if (
      !this.categoryChartCanvas
    ) {
      return;
    }

    this.categoryChart?.destroy();

    const categoryTotals =
      new Map<string, number>();

    for (
      const transaction
      of this.transactions()
    ) {

      if (
        transaction.type !== 2
      ) {
        continue;
      }

      const current =
        categoryTotals.get(
          transaction.categoryName
        ) ?? 0;

      categoryTotals.set(
        transaction.categoryName,
        current +
          transaction.amount
      );
    }

    const labels =
      [...categoryTotals.keys()];

    const values =
      labels.map(
        category =>
          categoryTotals.get(
            category
          ) ?? 0
      );

    this.categoryChart =
      new Chart(
        this.categoryChartCanvas
          .nativeElement,
        {
          type: 'doughnut',

          data: {
            labels,

            datasets: [
              {
                label:
                  this.i18n.t('Cheltuieli'),

                data:
                  values
              }
            ]
          },

          options: {
            responsive: true,
            maintainAspectRatio: false,

            plugins: {
              legend: {
                position: 'bottom',

                labels: {
                  color: '#d9dee7'
                }
              }
            }
          }
        }
      );
  }

  formatMoney(
    value: number
  ): string {

    return value.toLocaleString(
      this.i18n.locale(),
      {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
      }
    );
  }
}