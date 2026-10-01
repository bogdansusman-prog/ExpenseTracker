export function formatMoney(value: number): string {
  return value.toLocaleString('ro-RO', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  });
}

export function formatHours(hours: number | null): string {
  if (hours === null) {
    return '';
  }

  if (hours < 1) {
    return `${Math.max(1, Math.round(hours * 60))} min de munca`;
  }

  return `${hours.toLocaleString('ro-RO', { maximumFractionDigits: 1 })} h de munca`;
}

export const WEEK_DAYS_RO = ['Duminica', 'Luni', 'Marti', 'Miercuri', 'Joi', 'Vineri', 'Sambata'];

export const FREQUENCY_RO: Record<string, string> = {
  weekly: 'saptamanal',
  biweekly: 'la 2 saptamani',
  monthly: 'lunar',
  quarterly: 'trimestrial',
  yearly: 'anual'
};
