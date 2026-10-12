import { afterNextRender, Component, computed, DestroyRef, effect, ElementRef, inject, Injector, viewChild } from '@angular/core';
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
import { TranslatePipe } from './i18n/translate.pipe';
import { CINEMATIC_EASE, gsap, reducedMotion } from './shared/motion';

@Component({
  selector: 'app-root',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    AxCompanion,
    WelcomeSplash, TranslatePipe],
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

  private readonly nav = viewChild<ElementRef<HTMLElement>>('nav');
  private readonly indicator = viewChild<ElementRef<HTMLElement>>('indicator');
  private readonly sweep = viewChild<ElementRef<HTMLElement>>('sweep');
  private indicatorPlaced = false;
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);

  constructor() {
    // After every navigation: the nav pill glides to the new page and a line of light sweeps the top.
    effect(() => {
      this.url();
      const nav = this.nav();
      requestAnimationFrame(() => {
        this.moveIndicator(nav?.nativeElement, this.indicatorPlaced);
        this.playSweep();
      });
    });

    afterNextRender(() => {
      // Fonts, language switches and wrapping change the link sizes: keep the pill glued to its link.
      const observer = new ResizeObserver(() => this.moveIndicator(this.nav()?.nativeElement, false));
      const watch = effect(() => {
        observer.disconnect();
        const nav = this.nav()?.nativeElement;
        if (nav) {
          observer.observe(nav);
        }
      }, { injector: this.injector });
      this.destroyRef.onDestroy(() => {
        observer.disconnect();
        watch.destroy();
      });
    });
  }

  /** The blue pill behind the active link: one element that slides instead of each link lighting up. */
  private moveIndicator(nav: HTMLElement | undefined, animate: boolean, attempt = 0): void {
    const indicator = this.indicator()?.nativeElement;
    if (!nav || !indicator) {
      this.indicatorPlaced = false;
      return;
    }

    const active = nav.querySelector<HTMLElement>('a.active');
    if (!active && attempt < 5) {
      // routerLinkActive may not have marked the link yet (first load, guards): look again shortly.
      setTimeout(() => this.moveIndicator(nav, animate, attempt + 1), 120);
      return;
    }
    if (!active) {
      gsap.to(indicator, { autoAlpha: 0, scale: 0.8, duration: 0.3 });
      return;
    }

    nav.classList.add('has-indicator');
    const target = {
      x: active.offsetLeft,
      y: active.offsetTop,
      width: active.offsetWidth,
      height: active.offsetHeight,
      autoAlpha: 1,
      scale: 1
    };

    if (!animate || reducedMotion()) {
      gsap.set(indicator, target);
    } else {
      // stretch a little while travelling, like it has weight
      gsap.timeline()
        .to(indicator, { ...target, duration: 0.7, ease: CINEMATIC_EASE })
        .fromTo(indicator, { scaleY: 0.82 }, { scaleY: 1, duration: 0.6, ease: 'elastic.out(1, 0.45)' }, 0.12);
    }
    this.indicatorPlaced = true;
  }

  private playSweep(): void {
    const sweep = this.sweep()?.nativeElement;
    if (!sweep || reducedMotion()) {
      return;
    }

    gsap.timeline()
      .set(sweep, { autoAlpha: 1, scaleX: 0, transformOrigin: '0% 50%' })
      .to(sweep, { scaleX: 1, duration: 0.8, ease: CINEMATIC_EASE })
      .to(sweep, { autoAlpha: 0, duration: 0.5 }, '-=0.3');
  }
}
