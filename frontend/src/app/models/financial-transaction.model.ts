export type TransactionType = 1 | 2;

export interface FinancialTransaction {
  id: number;
  amount: number;
  date: string;
  type: TransactionType;
  description: string | null;
  categoryId: number;
  categoryName: string;
}

export interface FinancialTransactionRequest {
  amount: number;
  date: string | null;
  type: TransactionType;
  categoryId: number;
  description: string | null;
}