import { Injectable, signal } from '@angular/core';

import { EN } from './en';

export type Language = 'ro' | 'en';

const STORAGE_KEY = 'ax-tracker.language';

let currentLanguage: Language = readStoredLanguage();

/**
 * Lightweight translation: the Romanian text itself is the key, and the English
 * dictionary maps it to its translation. A missing entry simply falls back to Romanian.
 * Placeholders use {name}: t('Bine ai venit, {name}!', { name: 'Bogdan' }).
 */
@Injectable({
  providedIn: 'root'
})
export class I18n {
  readonly language = signal<Language>(currentLanguage);

  constructor() {
    if (typeof document !== 'undefined') {
      document.documentElement.lang = currentLanguage;
    }
  }

  readonly locale = () => (this.language() === 'en' ? 'en-US' : 'ro-RO');

  setLanguage(language: Language): void {
    const value: Language = language === 'en' ? 'en' : 'ro';
    currentLanguage = value;
    this.language.set(value);

    try {
      localStorage.setItem(STORAGE_KEY, value);
    } catch {
      // storage unavailable: keep it for this session only
    }

    if (typeof document !== 'undefined') {
      document.documentElement.lang = value;
    }
  }

  /** Reading language() here makes templates and computed values re-render on change. */
  t = (text: string, params?: Record<string, string | number>): string =>
    translate(text, this.language(), params);
}

export function translate(text: string, language: Language, params?: Record<string, string | number>): string {
  let result = language === 'en' ? (EN[text] ?? text) : text;

  if (params) {
    for (const [key, value] of Object.entries(params)) {
      result = result.split(`{${key}}`).join(String(value));
    }
  }

  return result;
}

/** For plain functions (formatters) that cannot inject the service. */
export function currentLang(): Language {
  return currentLanguage;
}

function readStoredLanguage(): Language {
  try {
    return localStorage.getItem(STORAGE_KEY) === 'en' ? 'en' : 'ro';
  } catch {
    return 'ro';
  }
}
