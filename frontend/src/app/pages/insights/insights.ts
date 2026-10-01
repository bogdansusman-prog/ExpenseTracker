import {
  Component,
  computed,
  ElementRef,
  inject,
  OnDestroy,
  OnInit,
  signal,
  ViewChild
} from '@angular/core';
import { forkJoin } from 'rxjs';
import Chart from 'chart.js/auto';

import {
  BalanceForecast,
  DetectedSubscription,
  PendingRegret,
  RegretSummary
} from '../../models/insights.model';
import { InsightsApiService } from '../../services/insights-api.service';
import { SettingsStore } from '../../services/settings.store';
import { formatHours, formatMoney, FREQUENCY_RO, WEEK_DAYS_RO } from '../../shared/format';
import { I18n } from '../../i18n/i18n.service';
import { TranslatePipe } from '../../i18n/translate.pipe';

interface HeatmapRow {
  category: string;
  cells: ({ score: number; count: number } | null)[];
}

@Component({
  selector: 'app-insights',
  imports: [TranslatePipe],
  templateUrl: './insights.html',
  styleUrl: './insights.scss'
})
export class Insights implements OnInit, OnDestroy {
  private readonly i18n = inject(I18n);

  private readonly api = inject(InsightsApiService);
  readonly settings = inject(SettingsStore);

  @ViewChild('forecastChart')
  private forecastCanvas?: ElementRef<HTMLCanvasElement>;

  private chart?: Chart;

  readonly loading = signal(true);
  readonly errorMessage = signal('');

  readonly pending = signal<PendingRegret[]>([]);
  readonly summary = signal<RegretSummary | null>(null);
  readonly subscriptions = signal<DetectedSubscription[]>([]);
  readonly forecast = signal<BalanceForecast | null>(null);
  readonly horizon = signal(30);

  readonly formatMoney = formatMoney;
  readonly formatHours = formatHours;
  readonly frequencyLabel = FREQUENCY_RO;

  /** Monday-first order for the heatmap columns. */
  readonly dayOrder = [1, 2, 3, 4, 5, 6, 0];
  readonly dayNames = WEEK_DAYS_RO;

  readonly subscriptionsMonthly = computed(() =>
    this.subscriptions().reduce((total, subscription) => total + subscription.monthlyCost, 0)
  );

  readonly priceIncreases = computed(() =>
    this.subscriptions().filter(subscription => subscription.priceIncreased)
  );

  readonly heatmap = computed<HeatmapRow[]>(() => {
    const summary = this.summary();

    if (!summary) {
      return [];
    }

    const categories = [...new Set(summary.heatmap.map(cell => cell.categoryName))];

    return categories.map(category => ({
      category,
      cells: this.dayOrder.map(day => {
        const cell = summary.heatmap.find(item => item.categoryName === category && item.dayOfWeek === day);
        return cell ? { score: cell.averageScore, count: cell.count } : null;
      })
    }));
  });

  ngOnInit(): void {
    this.settings.load();
    this.loadData();
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }

  loadData(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    forkJoin({
      pending: this.api.getPendingRegrets(),
      summary: this.api.getRegretSummary(),
      subscriptions: this.api.getSubscriptions(),
      forecast: this.api.getForecast(this.horizon())
    }).subscribe({
      next: data => {
        this.pending.set(data.pending);
        this.summary.set(data.summary);
        this.subscriptions.set(data.subscriptions);
        this.forecast.set(data.forecast);
        this.loading.set(false);
        setTimeout(() => this.renderChart());
      },
      error: error => {
        console.error('Insights error:', error);
        this.errorMessage.set('Datele nu au putut fi incarcate.');
        this.loading.set(false);
      }
    });
  }

  changeHorizon(days: number): void {
    this.horizon.set(days);
    this.api.getForecast(days).subscribe({
      next: forecast => {
        this.forecast.set(forecast);
        setTimeout(() => this.renderChart());
      }
    });
  }

  rate(item: PendingRegret, score: number): void {
    this.api.rateExpense(item.transactionId, score).subscribe({
      next: () => {
        this.pending.update(list => list.filter(other => other.transactionId !== item.transactionId));
        this.api.getRegretSummary().subscribe(summary => this.summary.set(summary));
      },
      error: () => this.errorMessage.set('Nota nu a putut fi salvata.')
    });
  }

  scoreColor(score: number): string {
    // 1 = red (regret) ... 5 = green (worth it)
    const hue = Math.round(((score - 1) / 4) * 120);
    return `hsl(${hue} 65% 38%)`;
  }

  hoursFor(amount: number): string {
    return formatHours(this.settings.workHours(amount));
  }

  private renderChart(): void {
    const forecast = this.forecast();
    const canvas = this.forecastCanvas?.nativeElement;

    if (!forecast || !canvas) {
      return;
    }

    this.chart?.destroy();

    const labels = forecast.points.map(point => point.date.slice(5));

    this.chart = new Chart(canvas, {
      type: 'line',
      data: {
        labels,
        datasets: [
          {
            label: this.i18n.t('Optimist (90%)'),
            data: forecast.points.map(point => point.optimistic),
            borderColor: 'rgba(81, 207, 102, 0.6)',
            backgroundColor: 'rgba(76, 110, 245, 0.15)',
            pointRadius: 0,
            borderWidth: 1,
            fill: '+2'
          },
          {
            label: this.i18n.t('Asteptat (mediana)'),
            data: forecast.points.map(point => point.expected),
            borderColor: '#748ffc',
            pointRadius: 0,
            borderWidth: 3,
            fill: false
          },
          {
            label: this.i18n.t('Pesimist (10%)'),
            data: forecast.points.map(point => point.pessimistic),
            borderColor: 'rgba(255, 107, 107, 0.6)',
            pointRadius: 0,
            borderWidth: 1,
            fill: false
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: {
          legend: { labels: { color: '#c4cad5' } },
          tooltip: {
            callbacks: {
              label: context => `${context.dataset.label}: ${formatMoney(Number(context.parsed.y))} lei`
            }
          }
        },
        scales: {
          x: { ticks: { color: '#9da5b1', maxTicksLimit: 10 }, grid: { color: '#22252b' } },
          y: { ticks: { color: '#9da5b1' }, grid: { color: '#22252b' } }
        }
      }
    });
  }
}
