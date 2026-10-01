import { inject, Injectable, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';

import { AppLanguage, AppSettings } from '../models/insights.model';
import { InsightsApiService } from './insights-api.service';

/**
 * App-wide settings (hourly rate, AI language) shared by every page through signals.
 */
@Injectable({
  providedIn: 'root'
})
export class SettingsStore {
  private readonly api = inject(InsightsApiService);

  readonly hourlyRate = signal<number | null>(null);
  readonly language = signal<AppLanguage>('ro');
  readonly loaded = signal(false);

  load(): void {
    if (this.loaded()) {
      return;
    }

    this.api.getSettings().subscribe({
      next: settings => this.apply(settings),
      error: () => this.loaded.set(true)
    });
  }

  save(settings: AppSettings): Observable<void> {
    return this.api.updateSettings(settings).pipe(
      tap(() => this.apply(settings))
    );
  }

  /** "45 lei" -> 2.3 when the hourly rate is 20 lei/h; null when no rate is set. */
  workHours(amount: number): number | null {
    const rate = this.hourlyRate();
    return rate && rate > 0 ? Math.round((amount / rate) * 10) / 10 : null;
  }

  private apply(settings: AppSettings): void {
    this.hourlyRate.set(settings.hourlyRate);
    this.language.set(settings.language === 'en' ? 'en' : 'ro');
    this.loaded.set(true);
  }
}
