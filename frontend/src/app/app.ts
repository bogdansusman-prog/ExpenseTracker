import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  NavigationEnd,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet
} from '@angular/router';
import { filter, map } from 'rxjs';

import { AuthService } from './auth/auth.service';
import { AxCompanion } from './components/ax-companion/ax-companion';
import { WelcomeSplash } from './components/welcome-splash/welcome-splash';
import { SplashService } from './components/welcome-splash/splash.service';

@Component({
  selector: 'app-root',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    AxCompanion,
    WelcomeSplash
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  readonly auth = inject(AuthService);
  readonly splash = inject(SplashService);
  private readonly router = inject(Router);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map(event => event.urlAfterRedirects)
    ),
    { initialValue: this.router.url }
  );

  /** Login and register are full-screen pages without the app header. */
  readonly isAuthPage = computed(() => /^\/(login|register)(\?|$)/.test(this.url()));
}
