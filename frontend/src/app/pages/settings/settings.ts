import { Component, effect, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { AppLanguage } from '../../models/insights.model';
import { SettingsStore } from '../../services/settings.store';
import { formatHours } from '../../shared/format';

@Component({
  selector: 'app-settings',
  imports: [FormsModule],
  templateUrl: './settings.html',
  styleUrl: './settings.scss'
})
export class Settings implements OnInit {
  readonly store = inject(SettingsStore);

  hourlyRate: number | null = null;
  language: AppLanguage = 'ro';

  /** Helper: compute the hourly rate from a monthly net salary (~168 working hours). */
  monthlySalary: number | null = null;

  readonly saving = signal(false);
  readonly message = signal('');
  readonly error = signal('');

  readonly formatHours = formatHours;

  constructor() {
    // Copy the loaded values into the form once they arrive from the API.
    effect(() => {
      if (this.store.loaded()) {
        this.hourlyRate = this.store.hourlyRate();
        this.language = this.store.language();
      }
    });
  }

  ngOnInit(): void {
    this.store.load();
  }

  fromSalary(): void {
    if (this.monthlySalary && this.monthlySalary > 0) {
      this.hourlyRate = Math.round((this.monthlySalary / 168) * 100) / 100;
    }
  }

  example(amount: number): string {
    const rate = Number(this.hourlyRate);
    return rate > 0 ? formatHours(Math.round((amount / rate) * 10) / 10) : '';
  }

  save(): void {
    this.saving.set(true);
    this.message.set('');
    this.error.set('');

    const rate = this.hourlyRate === null || `${this.hourlyRate}` === '' ? null : Number(this.hourlyRate);

    this.store.save({ hourlyRate: rate && rate > 0 ? rate : null, language: this.language }).subscribe({
      next: () => {
        this.message.set('Setarile au fost salvate.');
        this.saving.set(false);
      },
      error: () => {
        this.error.set('Setarile nu au putut fi salvate.');
        this.saving.set(false);
      }
    });
  }
}
