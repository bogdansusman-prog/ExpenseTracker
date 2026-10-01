import { currentLang, translate } from '../i18n/i18n.service';

function locale(): string {
  return currentLang() === 'en' ? 'en-US' : 'ro-RO';
}

export function formatMoney(value: number): string {
  return value.toLocaleString(locale(), {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  });
}

export function formatHours(hours: number | null): string {
  if (hours === null) {
    return '';
  }

  if (hours < 1) {
    return translate('{n} min de munca', currentLang(), { n: Math.max(1, Math.round(hours * 60)) });
  }

  return translate('{n} h de munca', currentLang(), {
    n: hours.toLocaleString(locale(), { maximumFractionDigits: 1 })
  });
}

/** Romanian names; translate them with the `t` pipe in templates. */
export const WEEK_DAYS_RO = ['Duminica', 'Luni', 'Marti', 'Miercuri', 'Joi', 'Vineri', 'Sambata'];

export const FREQUENCY_RO: Record<string, string> = {
  weekly: 'saptamanal',
  biweekly: 'la 2 saptamani',
  monthly: 'lunar',
  quarterly: 'trimestrial',
  yearly: 'anual'
};
