import { Directive, effect, ElementRef, inject, input, OnDestroy, untracked } from '@angular/core';

import { gsap, reducedMotion } from './motion';

/**
 * A number that counts up to its value (and glides to the new one when it changes).
 * The element's text is written by the directive, so leave it empty:
 *
 *   <span [axCountUp]="balance()" [countFormat]="money"></span> lei
 */
@Directive({ selector: '[axCountUp]' })
export class CountUp implements OnDestroy {
  readonly axCountUp = input.required<number>();
  readonly countFormat = input<(value: number) => string>(value => Math.round(value).toString());
  readonly countDuration = input(1.8);

  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly state = { value: 0 };
  private tween?: gsap.core.Tween;
  private first = true;

  constructor() {
    effect(() => {
      const target = this.axCountUp();
      const format = this.countFormat();
      // Formatting here (tracked) also re-runs the effect when the language / locale changes.
      const finalText = format(target);
      untracked(() => this.animateTo(target, format, finalText));
    });
  }

  private animateTo(target: number, format: (value: number) => string, finalText: string): void {
    this.tween?.kill();

    if (reducedMotion() || target === this.state.value) {
      this.state.value = target;
      this.element.textContent = finalText;
      this.first = false;
      return;
    }

    if (this.first) {
      this.element.textContent = format(0);
    }

    this.tween = gsap.to(this.state, {
      value: target,
      duration: this.first ? this.countDuration() : 0.9,
      delay: this.first ? 0.2 : 0,
      ease: 'expo.out',
      onUpdate: () => { this.element.textContent = format(this.state.value); },
      onComplete: () => { this.element.textContent = finalText; }
    });
    this.first = false;
  }

  ngOnDestroy(): void {
    this.tween?.kill();
  }
}
