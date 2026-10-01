import { inject, Injectable, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';

import { I18n } from '../i18n/i18n.service';
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
  private readonly i18n = inject(I18n);

  readonly hourlyRate = signal<number | null>(null);
  /** Same signal as the UI language: one switch translates the app and Ax. */
  readonly language = this.i18n.language;
  readonly loaded = signal(false);

  load(): void {
    if (this.loaded()) {
      return;
    }

    this.api.getSettings().subscribe({
      next: settings => this.applyFromServer(settings),
      error: () => this.loaded.set(true)
    });
  }

  /** Switch the language right away (UI + Ax) and remember it on the server. */
  setLanguage(language: AppLanguage): void {
    this.i18n.setLanguage(language);
    this.api.updateSettings({ hourlyRate: this.hourlyRate(), language }).subscribe({ error: () => undefined });
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

  /**
   * The language picked on this device wins (it is what the user is looking at),
   * so if the server remembers another one we update the server instead.
   */
  private applyFromServer(settings: AppSettings): void {
    const local = this.i18n.language();
    if (settings.language !== local) {
      this.api.updateSettings({ hourlyRate: settings.hourlyRate, language: local }).subscribe({ error: () => undefined });
    }
    this.hourlyRate.set(settings.hourlyRate);
    this.loaded.set(true);
  }

  private apply(settings: AppSettings): void {
    this.hourlyRate.set(settings.hourlyRate);
    this.i18n.setLanguage(settings.language === 'en' ? 'en' : 'ro');
    this.loaded.set(true);
  }
}
