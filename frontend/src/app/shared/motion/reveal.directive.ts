import { afterNextRender, Directive, ElementRef, inject, input, OnDestroy } from '@angular/core';

import { CINEMATIC_EASE, gsap, reducedMotion } from './motion';

/**
 * Cinematic entrance: the children (or the elements matching the selector) rise out of a blur,
 * one after another, when they come into view.
 *
 *   <section axReveal>…</section>                      direct children
 *   <div axReveal=".summary-card, .transaction-item">   any descendants, in document order
 *
 * An IntersectionObserver decides when (not scroll events): it also notices elements that move
 * into view because something above them disappeared, e.g. after a reload of the page data.
 */
@Directive({ selector: '[axReveal]' })
export class Reveal implements OnDestroy {
  readonly axReveal = input<string>('');
  /** Seconds to wait before the first item, e.g. to let the page transition finish. */
  readonly revealDelay = input(0);

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private observer?: IntersectionObserver;
  private context?: gsap.Context;

  constructor() {
    afterNextRender(() => this.play());
  }

  private play(): void {
    if (reducedMotion() || typeof IntersectionObserver === 'undefined') {
      return;
    }

    const root = this.host.nativeElement;
    const selector = this.axReveal().trim();
    const items = (selector
      ? Array.from(root.querySelectorAll<HTMLElement>(selector))
      : Array.from(root.children) as HTMLElement[]
    ).slice(0, 80); // long tables: only the first rows are worth animating

    if (items.length === 0) {
      return;
    }

    this.context = gsap.context(() => {
      gsap.set(items, {
        autoAlpha: 0,
        y: 46,
        rotationX: -12,
        transformPerspective: 900,
        transformOrigin: '50% 0%',
        filter: 'blur(10px)'
      });
    }, root);

    // Everything that becomes visible in the same moment is revealed as one staggered group.
    this.observer = new IntersectionObserver(entries => {
      const entering = entries
        .filter(entry => entry.isIntersecting)
        .map(entry => entry.target as HTMLElement)
        .sort((a, b) => items.indexOf(a) - items.indexOf(b));

      if (entering.length === 0) {
        return;
      }

      entering.forEach(element => this.observer?.unobserve(element));
      this.context?.add(() => {
        gsap.to(entering, {
          autoAlpha: 1,
          y: 0,
          rotationX: 0,
          filter: 'blur(0px)',
          duration: 1.1,
          ease: CINEMATIC_EASE,
          stagger: 0.07,
          delay: this.revealDelay(),
          overwrite: true,
          // hand the element back to CSS (hover effects, tilt) once it has landed
          clearProps: 'transform,transformOrigin,filter,visibility,opacity'
        });
      });
    }, { rootMargin: '0px 0px -6% 0px' });

    items.forEach(item => this.observer?.observe(item));
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
    this.context?.revert();
  }
}
