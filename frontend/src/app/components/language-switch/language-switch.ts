import { Component, inject } from '@angular/core';

import { I18n, Language } from '../../i18n/i18n.service';
import { SettingsStore } from '../../services/settings.store';
import { AuthService } from '../../auth/auth.service';

/** Small RO / EN pill, used where the Settings page is not reachable (login, register). */
@Component({
  selector: 'app-language-switch',
  template: `
    <div class="switch" role="group" aria-label="Language">
      <button type="button" [class.active]="i18n.language() === 'ro'" (click)="set('ro')">RO</button>
      <button type="button" [class.active]="i18n.language() === 'en'" (click)="set('en')">EN</button>
    </div>
  `,
  styles: `
    .switch {
      display: inline-flex;
      padding: 3px;
      background: #1a1c20;
      border: 1px solid #2c3038;
      border-radius: 999px;
    }
    button {
      padding: 5px 12px;
      font-size: 12px;
      font-weight: 700;
      letter-spacing: 0.04em;
      color: #9da5b1;
      background: transparent;
      border: 0;
      border-radius: 999px;
      cursor: pointer;
      transition: background 0.15s, color 0.15s;
    }
    button.active {
      color: #fff;
      background: linear-gradient(135deg, #f783ac, #9775fa);
    }
  `
})
export class LanguageSwitch {
  readonly i18n = inject(I18n);
  private readonly settings = inject(SettingsStore);
  private readonly auth = inject(AuthService);

  set(language: Language): void {
    if (this.auth.isLoggedIn()) {
      this.settings.setLanguage(language);
    } else {
      this.i18n.setLanguage(language);
    }
  }
}
