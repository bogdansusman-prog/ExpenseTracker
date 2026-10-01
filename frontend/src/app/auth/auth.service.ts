import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';

import { AuthResponse, AuthUser, LoginRequest, RegisterRequest } from './auth.model';

const STORAGE_KEY = 'expense-tracker.session';

interface StoredSession {
  token: string;
  expiresAt: string;
  user: AuthUser;
}

/**
 * Holds the logged-in user and the JWT. The session is kept in localStorage
 * so a page refresh doesn't log you out (until the token expires).
 */
@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly apiUrl = 'https://localhost:7007/api/auth';

  private readonly session = signal<StoredSession | null>(AuthService.restore());

  readonly user = computed(() => this.session()?.user ?? null);
  readonly isLoggedIn = computed(() => this.session() !== null);

  get token(): string | null {
    const session = this.session();

    if (!session) {
      return null;
    }

    if (new Date(session.expiresAt).getTime() <= Date.now()) {
      this.clear();
      return null;
    }

    return session.token;
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.apiUrl}/login`, request)
      .pipe(tap(response => this.store(response)));
  }

  // ----- Profile -----

  refreshMe(): Observable<AuthUser> {
    return this.http.get<AuthUser>(`${this.apiUrl}/me`).pipe(tap(user => this.updateUser(user)));
  }

  updateProfile(displayName: string): Observable<AuthUser> {
    return this.http.put<AuthUser>(`${this.apiUrl}/profile`, { displayName }).pipe(tap(user => this.updateUser(user)));
  }

  updateAvatar(dataUrl: string): Observable<AuthUser> {
    return this.http.put<AuthUser>(`${this.apiUrl}/avatar`, { dataUrl }).pipe(tap(user => this.updateUser(user)));
  }

  deleteAvatar(): Observable<AuthUser> {
    return this.http.delete<AuthUser>(`${this.apiUrl}/avatar`).pipe(tap(user => this.updateUser(user)));
  }

  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/change-password`, { currentPassword, newPassword });
  }

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.apiUrl}/register`, request)
      .pipe(tap(response => this.store(response)));
  }

  logout(): void {
    this.clear();
    // Full reload clears every cached signal (settings, lists) from the previous user.
    window.location.href = '/login';
  }

  /** Called by the interceptor when the API answers 401 (expired or invalid token). */
  handleUnauthorized(): void {
    this.clear();
    this.router.navigate(['/login'], { queryParams: { expired: 1 } });
  }

  /** Keeps the token, replaces the user data (after a profile change). */
  private updateUser(user: AuthUser): void {
    const session = this.session();
    if (!session) {
      return;
    }

    this.persist({ ...session, user });
  }

  private store(response: AuthResponse): void {
    const session: StoredSession = {
      token: response.token,
      expiresAt: response.expiresAt,
      user: response.user
    };

    this.persist(session);
  }

  private persist(session: StoredSession): void {
    this.session.set(session);

    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    } catch {
      // storage unavailable (private mode): the session lives only in memory
    }
  }

  private clear(): void {
    this.session.set(null);

    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // ignore
    }
  }

  private static restore(): StoredSession | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) {
        return null;
      }

      const session = JSON.parse(raw) as StoredSession;
      return new Date(session.expiresAt).getTime() > Date.now() ? session : null;
    } catch {
      return null;
    }
  }
}
