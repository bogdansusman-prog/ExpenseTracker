import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import {
  Category,
  CategoryRequest
} from '../models/category.model';

import {
  FinancialTransaction,
  FinancialTransactionRequest
} from '../models/financial-transaction.model';

@Injectable({
  providedIn: 'root'
})
export class ExpenseApiService {
  private readonly http = inject(HttpClient);

  private readonly apiUrl = 'https://localhost:7007/api';

  getCategories(): Observable<Category[]> {
    return this.http.get<Category[]>(
      `${this.apiUrl}/categories`
    );
  }

  getCategory(id: number): Observable<Category> {
    return this.http.get<Category>(
      `${this.apiUrl}/categories/${id}`
    );
  }

  createCategory(request: CategoryRequest): Observable<Category> {
    return this.http.post<Category>(
      `${this.apiUrl}/categories`,
      request
    );
  }

  updateCategory(
    id: number,
    request: CategoryRequest
  ): Observable<void> {
    return this.http.put<void>(
      `${this.apiUrl}/categories/${id}`,
      request
    );
  }

  deleteCategory(id: number): Observable<void> {
    return this.http.delete<void>(
      `${this.apiUrl}/categories/${id}`
    );
  }

  getTransactions(): Observable<FinancialTransaction[]> {
    return this.http.get<FinancialTransaction[]>(
      `${this.apiUrl}/transactions`
    );
  }

  getTransaction(id: number): Observable<FinancialTransaction> {
    return this.http.get<FinancialTransaction>(
      `${this.apiUrl}/transactions/${id}`
    );
  }

  createTransaction(
    request: FinancialTransactionRequest
  ): Observable<FinancialTransaction> {
    return this.http.post<FinancialTransaction>(
      `${this.apiUrl}/transactions`,
      request
    );
  }

  updateTransaction(
    id: number,
    request: FinancialTransactionRequest
  ): Observable<void> {
    return this.http.put<void>(
      `${this.apiUrl}/transactions/${id}`,
      request
    );
  }

  deleteTransaction(id: number): Observable<void> {
    return this.http.delete<void>(
      `${this.apiUrl}/transactions/${id}`
    );
  }
}