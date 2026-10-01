import { inject, Pipe, PipeTransform } from '@angular/core';

import { I18n } from './i18n.service';

/**
 * {{ 'Tranzactii' | t }} → "Transactions" when the app language is English.
 * Impure on purpose: it re-runs when the language signal changes.
 */
@Pipe({
  name: 't',
  pure: false
})
export class TranslatePipe implements PipeTransform {
  private readonly i18n = inject(I18n);

  transform(text: string, params?: Record<string, string | number>): string {
    return this.i18n.t(text, params);
  }
}
