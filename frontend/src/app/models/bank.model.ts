import { TransactionType } from './financial-transaction.model';

export interface BankAccount {
  id: number;
  iban: string | null;
  currency: string | null;
  name: string | null;
}

export interface BankConnection {
  id: number;
  aspspName: string;
  aspspCountry: string;
  validUntil: string;
  expired: boolean;
  createdAt: string;
  lastSyncedAt: string | null;
  accounts: BankAccount[];
}

export interface BankStatus {
  configured: boolean;
  redirectUrl: string;
  connections: BankConnection[];
}

export interface BankOption {
  name: string;
  country: string;
  logo: string | null;
}

export interface BankPreviewItem {
  externalId: string;
  date: string;
  amount: number;
  type: TransactionType;
  description: string;
  currency: string;
  accountLabel: string | null;
  suggestedCategoryId: number | null;
  alreadyImported: boolean;
}

export interface BankPreview {
  from: string;
  to: string;
  items: BankPreviewItem[];
}

export interface BankImportItem {
  externalId: string;
  date: string;
  amount: number;
  type: TransactionType;
  description: string | null;
  categoryId: number;
}

export interface BankImportResult {
  imported: number;
  skipped: number;
}
