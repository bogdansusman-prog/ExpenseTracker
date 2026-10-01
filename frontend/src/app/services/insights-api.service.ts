import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import {
  AdvisorReport,
  AdvisorStatus,
  AppLanguage,
  AppSettings,
  BalanceForecast,
  ChatTurn,
  DetectedSubscription,
  PendingRegret,
  QuickAddResult,
  RegretSummary
} from '../models/insights.model';

@Injectable({
  providedIn: 'root'
})
export class InsightsApiService {
  private readonly http = inject(HttpClient);

  private readonly apiUrl = 'https://localhost:7007/api';

  // Settings
  getSettings(): Observable<AppSettings> {
    return this.http.get<AppSettings>(`${this.apiUrl}/settings`);
  }

  updateSettings(settings: AppSettings): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/settings`, settings);
  }

  // Regret score
  getPendingRegrets(): Observable<PendingRegret[]> {
    return this.http.get<PendingRegret[]>(`${this.apiUrl}/insights/regret/pending`);
  }

  rateExpense(transactionId: number, score: number): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/insights/regret/${transactionId}`, { score });
  }

  getRegretSummary(): Observable<RegretSummary> {
    return this.http.get<RegretSummary>(`${this.apiUrl}/insights/regret/summary`);
  }

  // Subscriptions & forecast
  getSubscriptions(): Observable<DetectedSubscription[]> {
    return this.http.get<DetectedSubscription[]>(`${this.apiUrl}/insights/subscriptions`);
  }

  getForecast(days = 30): Observable<BalanceForecast> {
    const params = new HttpParams().set('days', days);
    return this.http.get<BalanceForecast>(`${this.apiUrl}/insights/forecast`, { params });
  }

  // Quick add
  parseQuickAdd(text: string, useAi = true): Observable<QuickAddResult> {
    return this.http.post<QuickAddResult>(`${this.apiUrl}/quickadd/parse`, { text, useAi });
  }

  // AI advisor
  getAdvisorStatus(): Observable<AdvisorStatus> {
    return this.http.get<AdvisorStatus>(`${this.apiUrl}/advisor/status`);
  }

  getAdvisorReport(language: AppLanguage, refresh = false): Observable<AdvisorReport> {
    const params = new HttpParams().set('language', language).set('refresh', refresh);
    return this.http.get<AdvisorReport>(`${this.apiUrl}/advisor/report`, { params });
  }

  chat(messages: ChatTurn[], language: AppLanguage): Observable<{ reply: string }> {
    return this.http.post<{ reply: string }>(`${this.apiUrl}/advisor/chat`, { messages, language });
  }
}
