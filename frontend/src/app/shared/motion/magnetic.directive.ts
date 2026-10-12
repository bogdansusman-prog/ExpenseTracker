import { afterNextRender, DestroyRef, Directive, ElementRef, inject, input } from '@angular/core';

import { finePointer, gsap, reducedMotion } from './motion';

/** Button that is gently pulled towards the mouse, then springs back. Mouse only. */
@Directive({ selector: '[axMagnetic]' })
export class Magnetic {
  /** How strongly it follows the mouse (0..1). */
  readonly magnetStrength = input(0.3);

  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly destroyRef = inject(DestroyRef);

  constructor() {
    afterNextRender(() => {
      if (reducedMotion() || !finePointer()) {
        return;
      }

      const element = this.element;
      const toX = gsap.quickTo(element, 'x', { duration: 0.5, ease: 'power3.out' });
      const toY = gsap.quickTo(element, 'y', { duration: 0.5, ease: 'power3.out' });

      const move = (event: PointerEvent) => {
        const box = element.getBoundingClientRect();
        toX((event.clientX - (box.left + box.width / 2)) * this.magnetStrength());
        toY((event.clientY - (box.top + box.height / 2)) * this.magnetStrength());
      };
      const leave = () => {
        gsap.to(element, { x: 0, y: 0, duration: 0.8, ease: 'elastic.out(1, 0.4)' });
      };

      element.addEventListener('pointermove', move);
      element.addEventListener('pointerleave', leave);
      this.destroyRef.onDestroy(() => {
        element.removeEventListener('pointermove', move);
        element.removeEventListener('pointerleave', leave);
        gsap.killTweensOf(element);
      });
    });
  }
}
