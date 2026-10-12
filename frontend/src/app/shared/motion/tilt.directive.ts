import { afterNextRender, DestroyRef, Directive, ElementRef, inject, input } from '@angular/core';

import { finePointer, gsap, reducedMotion } from './motion';

/**
 * Card that tilts in 3D towards the mouse, with a soft light that follows the cursor
 * (the light is the `.ax-tilt::after` style in styles.scss). Mouse only, never on touch.
 */
@Directive({
  selector: '[axTilt]',
  host: { class: 'ax-tilt' }
})
export class Tilt {
  /** Maximum rotation in degrees. */
  readonly tiltMax = input(7);

  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly destroyRef = inject(DestroyRef);

  constructor() {
    afterNextRender(() => this.setup());
  }

  private setup(): void {
    if (reducedMotion() || !finePointer()) {
      return;
    }

    const element = this.element;
    gsap.set(element, { transformPerspective: 900 });
    const rotateX = gsap.quickTo(element, 'rotationX', { duration: 0.6, ease: 'power3.out' });
    const rotateY = gsap.quickTo(element, 'rotationY', { duration: 0.6, ease: 'power3.out' });

    const move = (event: PointerEvent) => {
      const box = element.getBoundingClientRect();
      const x = (event.clientX - box.left) / box.width;
      const y = (event.clientY - box.top) / box.height;
      rotateY((x - 0.5) * 2 * this.tiltMax());
      rotateX((0.5 - y) * 2 * this.tiltMax());
      element.style.setProperty('--mx', `${x * 100}%`);
      element.style.setProperty('--my', `${y * 100}%`);
    };

    const enter = () => {
      element.style.setProperty('--glare', '1');
      gsap.to(element, { scale: 1.025, duration: 0.5, ease: 'power3.out' });
    };

    const leave = () => {
      element.style.setProperty('--glare', '0');
      rotateX(0);
      rotateY(0);
      gsap.to(element, { scale: 1, duration: 0.7, ease: 'elastic.out(1, 0.5)' });
    };

    element.addEventListener('pointerenter', enter);
    element.addEventListener('pointermove', move);
    element.addEventListener('pointerleave', leave);

    this.destroyRef.onDestroy(() => {
      element.removeEventListener('pointerenter', enter);
      element.removeEventListener('pointermove', move);
      element.removeEventListener('pointerleave', leave);
      gsap.killTweensOf(element);
    });
  }
}
