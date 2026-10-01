import { TransactionType } from './financial-transaction.model';

export type AppLanguage = 'ro' | 'en';

export interface AppSettings {
  hourlyRate: number | null;
  language: AppLanguage;
}

export interface PendingRegret {
  transactionId: number;
  date: string;
  amount: number;
  categoryName: string;
  description: string | null;
  daysAgo: number;
  workHours: number | null;
}

export interface CategoryRegret {
  categoryName: string;
  averageScore: number;
  count: number;
  amount: number;
  regrettedAmount: number;
}

export interface RegretCell {
  categoryName: string;
  dayOfWeek: number;
  averageScore: number;
  count: number;
  amount: number;
}

export interface RegretSummary {
  ratedCount: number;
  averageScore: number;
  regrettedAmountThisMonth: number;
  regrettedAmountTotal: number;
  worstCategory: string | null;
  worstDayOfWeek: number | null;
  byCategory: CategoryRegret[];
  heatmap: RegretCell[];
}

export interface DetectedSubscription {
  name: string;
  categoryName: string;
  frequency: 'weekly' | 'biweekly' | 'monthly' | 'quarterly' | 'yearly';
  intervalDays: number;
  occurrences: number;
  currentAmount: number;
  previousAmount: number | null;
  priceChangePercent: number;
  priceIncreased: boolean;
  monthlyCost: number;
  yearlyCost: number;
  lastChargeDate: string;
  nextExpectedDate: string;
}

export interface ForecastPoint {
  date: string;
  pessimistic: number;
  expected: number;
  optimistic: number;
}

export interface BalanceForecast {
  currentBalance: number;
  from: string;
  to: string;
  simulations: number;
  historyDays: number;
  endPessimistic: number;
  endExpected: number;
  endOptimistic: number;
  probabilityNegative: number;
  points: ForecastPoint[];
}

export interface ParsedTransaction {
  amount: number | null;
  type: TransactionType;
  date: string;
  categoryId: number | null;
  categoryName: string | null;
  description: string | null;
  confidence: number;
  missingFields: string[];
}

export interface QuickAddResult {
  draft: ParsedTransaction;
  source: 'rules';
  workHours: number | null;
}

export interface AdvisorAction {
  title: string;
  detail: string;
  estimatedMonthlySavings: number | null;
}

export interface ScoreComponent {
  key: string;
  label: string;
  points: number;
  maxPoints: number;
  explanation: string;
}

export interface AdvisorReport {
  score: number;
  verdict: 'good' | 'ok' | 'bad';
  headline: string;
  summary: string;
  strengths: string[];
  concerns: string[];
  actions: AdvisorAction[];
  funFact: string | null;
  breakdown: ScoreComponent[];
  language: AppLanguage;
  generatedAt: string;
  engine: string;
}

export interface AdvisorStatus {
  configured: boolean;
  engine: string;
}

export interface ChatTurn {
  role: 'user' | 'assistant';
  content: string;
}
