import { Injectable, signal } from '@angular/core';

/**
 * Plays the full-screen "welcome" animation after login / register.
 * It lives at the app root, so the dashboard can already load behind it.
 */
@Injectable({
  providedIn: 'root'
})
export class SplashService {
  readonly active = signal<{ name: string } | null>(null);

  play(name: string): void {
    this.active.set({ name });
  }

  finish(): void {
    this.active.set(null);
  }
}
