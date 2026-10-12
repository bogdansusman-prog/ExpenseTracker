import { afterNextRender, Component, computed, DestroyRef, ElementRef, inject, input, signal, viewChild } from '@angular/core';

import { I18n } from '../../i18n/i18n.service';
import { finePointer, gsap, reducedMotion } from '../../shared/motion';

export type AxMood = 'idle' | 'thinking' | 'nodding' | 'happy' | 'worried' | 'alert';

let instanceCounter = 0;

/**
 * Ax, the Ax Tracker mascot: an axolotl with round glasses and a bow tie.
 * SVG + CSS animations for the moods, plus a bit of GSAP life:
 * he pops in when he appears, his eyes follow the mouse, he squashes and hops when clicked,
 * and five quick clicks make him do a happy spin with hearts.
 */
@Component({
  selector: 'app-ax',
  templateUrl: './ax-mascot.html',
  styleUrl: './ax-mascot.scss',
  host: { '(click)': 'boop()' }
})
export class AxMascot {
  readonly mood = input<AxMood>('idle');
  readonly size = input<number>(160);

  /** Short-lived mood from the easter egg; wins over the input while it lasts. */
  readonly override = signal<AxMood | null>(null);

  /** Gradient ids must be unique per instance when several Ax are on the same page. */
  private readonly uid = `ax-${++instanceCounter}`;

  readonly ids = {
    skin: `${this.uid}-skin`,
    gill: `${this.uid}-gill`,
    lens: `${this.uid}-lens`
  };

  readonly fills = {
    skin: `url(#${this.ids.skin})`,
    gill: `url(#${this.ids.gill})`,
    lens: `url(#${this.ids.lens})`
  };

  readonly heartSlots = Array.from({ length: 7 });

  private readonly i18n = inject(I18n);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly destroyRef = inject(DestroyRef);
  private readonly look = viewChild<ElementRef<SVGGElement>>('look');
  private readonly hearts = viewChild<ElementRef<SVGGElement>>('hearts');

  private clicks: number[] = [];
  private partyTimer?: ReturnType<typeof setTimeout>;

  readonly label = computed(() => {
    const labels: Record<AxMood, string> = {
      idle: 'Ax, mascota Ax Tracker',
      thinking: 'Ax se gandeste',
      nodding: 'Ax raspunde',
      happy: 'Ax este fericit',
      worried: 'Ax este ingrijorat',
      alert: 'Ax are ceva important de spus'
    };
    return this.i18n.t(labels[this.override() ?? this.mood()]);
  });

  constructor() {
    afterNextRender(() => {
      if (reducedMotion()) {
        return;
      }

      // Pop in, like he just jumped into the page.
      gsap.from(this.host, {
        scale: 0.6,
        y: 14,
        autoAlpha: 0,
        duration: 0.9,
        ease: 'back.out(1.8)',
        transformOrigin: '50% 100%',
        clearProps: 'transform,opacity,visibility'
      });

      if (finePointer()) {
        this.followMouse();
      }
    });

    this.destroyRef.onDestroy(() => {
      clearTimeout(this.partyTimer);
      gsap.killTweensOf(this.host);
    });
  }

  /** Click: squash and hop. Five clicks in under two seconds: a spin and a burst of hearts. */
  boop(): void {
    if (reducedMotion()) {
      return;
    }

    const now = performance.now();
    this.clicks = [...this.clicks.filter(time => now - time < 2000), now];

    if (this.clicks.length >= 5) {
      this.clicks = [];
      this.party();
      return;
    }

    gsap.timeline({ defaults: { transformOrigin: '50% 100%' } })
      .to(this.host, { scaleX: 1.12, scaleY: 0.86, duration: 0.1, ease: 'power2.out' })
      .to(this.host, { scaleX: 0.94, scaleY: 1.08, y: -16, duration: 0.22, ease: 'power2.out' })
      .to(this.host, { scaleX: 1, scaleY: 1, y: 0, duration: 0.5, ease: 'bounce.out' });
  }

  private party(): void {
    this.override.set('happy');
    clearTimeout(this.partyTimer);
    this.partyTimer = setTimeout(() => this.override.set(null), 2600);

    gsap.timeline({ defaults: { transformOrigin: '50% 60%' } })
      .to(this.host, { y: -22, scale: 1.1, duration: 0.25, ease: 'power2.out' })
      .to(this.host, { rotation: 360, duration: 0.7, ease: 'power2.inOut' }, 0.1)
      .to(this.host, { y: 0, scale: 1, duration: 0.6, ease: 'bounce.out' })
      .set(this.host, { rotation: 0 });

    const hearts = this.hearts()?.nativeElement.querySelectorAll('path');
    if (!hearts) {
      return;
    }

    hearts.forEach((heart, index) => {
      const angle = -Math.PI / 2 + (index - 3) * 0.42;
      gsap.fromTo(heart,
        { x: 110, y: 70, scale: 0.3, opacity: 1, rotation: 0 },
        {
          x: 110 + Math.cos(angle) * (70 + (index % 3) * 18),
          y: 70 + Math.sin(angle) * (70 + (index % 2) * 20),
          scale: 1.2 + (index % 3) * 0.25,
          rotation: (index - 3) * 12,
          opacity: 0,
          duration: 1.4,
          delay: index * 0.04,
          ease: 'power2.out'
        });
    });
  }

  /** The eyes look towards the mouse, a few units at most, so he seems to watch what you do. */
  private followMouse(): void {
    const look = this.look()?.nativeElement;
    if (!look) {
      return;
    }

    const toX = gsap.quickTo(look, 'x', { duration: 0.5, ease: 'power3.out' });
    const toY = gsap.quickTo(look, 'y', { duration: 0.5, ease: 'power3.out' });

    const move = (event: PointerEvent) => {
      const box = this.host.getBoundingClientRect();
      if (box.width === 0) {
        return;
      }
      const dx = event.clientX - (box.left + box.width / 2);
      const dy = event.clientY - (box.top + box.height * 0.42);
      const distance = Math.hypot(dx, dy) || 1;
      const reach = Math.min(distance / 260, 1) * 3.4;
      toX((dx / distance) * reach);
      toY((dy / distance) * reach * 0.8);
    };

    window.addEventListener('pointermove', move, { passive: true });
    this.destroyRef.onDestroy(() => {
      window.removeEventListener('pointermove', move);
      gsap.killTweensOf(look);
    });
  }
}
