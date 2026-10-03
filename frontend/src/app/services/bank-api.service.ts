import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import {
  BankConnection,
  BankImportItem,
  BankImportResult,
  BankOption,
  BankPreview,
  BankStatus
} from '../models/bank.model';

/** Bank connection through Enable Banking: the API does all the talking to the bank. */
@Injectable({
  providedIn: 'root'
})
export class BankApiService {
  private readonly http = inject(HttpClient);

  private readonly apiUrl = 'https://localhost:7007/api/bank';

  getStatus(): Observable<BankStatus> {
    return this.http.get<BankStatus>(`${this.apiUrl}/status`);
  }

  getBanks(country = 'RO'): Observable<BankOption[]> {
    return this.http.get<BankOption[]>(`${this.apiUrl}/banks`, { params: new HttpParams().set('country', country) });
  }

  connect(aspspName: string, country = 'RO'): Observable<{ url: string }> {
    return this.http.post<{ url: string }>(`${this.apiUrl}/connect`, { aspspName, country });
  }

  completeCallback(code: string, state: string): Observable<BankConnection> {
    return this.http.post<BankConnection>(`${this.apiUrl}/callback`, { code, state });
  }

  preview(connectionId: number, days: number | null): Observable<BankPreview> {
    let params = new HttpParams();
    if (days) {
      params = params.set('days', days);
    }
    return this.http.get<BankPreview>(`${this.apiUrl}/connections/${connectionId}/preview`, { params });
  }

  import(connectionId: number, items: BankImportItem[]): Observable<BankImportResult> {
    return this.http.post<BankImportResult>(`${this.apiUrl}/connections/${connectionId}/import`, { items });
  }

  disconnect(connectionId: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/connections/${connectionId}`);
  }
}
