import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';

import { SplashService } from './splash.service';

interface Bubble {
  left: number;
  size: number;
  delay: number;
  duration: number;
}

@Component({
  selector: 'app-welcome-splash',
  templateUrl: './welcome-splash.html',
  styleUrl: './welcome-splash.scss'
})
export class WelcomeSplash implements OnInit, OnDestroy {
  private readonly splash = inject(SplashService);

  readonly leaving = signal(false);
  readonly reduceMotion = typeof window !== 'undefined'
    && !!window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;

  readonly firstName = computed(() => (this.splash.active()?.name ?? '').trim().split(/\s+/)[0] || 'prietene');

  /** One span per character so the greeting can appear letter by letter. */
  readonly letters = computed(() =>
    Array.from(`Bine ai venit, ${this.firstName()}!`).map(character => (character === ' ' ? ' ' : character))
  );

  readonly steps = ['Iti aduc tranzactiile', 'Calculez prognoza', 'Il trezesc pe Ax'];

  readonly bubbles: Bubble[] = Array.from({ length: 18 }, (_, index) => ({
    left: (index * 37) % 100,
    size: 6 + ((index * 13) % 22),
    delay: (index % 6) * 0.25,
    duration: 2.4 + ((index * 7) % 10) / 5
  }));

  private timers: ReturnType<typeof setTimeout>[] = [];

  ngOnInit(): void {
    const visible = this.reduceMotion ? 700 : 2900;
    const exit = this.reduceMotion ? 200 : 650;

    this.timers.push(setTimeout(() => this.leaving.set(true), visible));
    this.timers.push(setTimeout(() => this.splash.finish(), visible + exit));
  }

  ngOnDestroy(): void {
    this.timers.forEach(clearTimeout);
  }

  skip(): void {
    this.timers.forEach(clearTimeout);
    this.leaving.set(true);
    this.timers = [setTimeout(() => this.splash.finish(), 350)];
  }
}
