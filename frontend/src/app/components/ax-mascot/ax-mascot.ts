import { inject, Component, computed, input } from '@angular/core';

import { I18n } from '../../i18n/i18n.service';

export type AxMood = 'idle' | 'thinking' | 'nodding' | 'happy' | 'worried' | 'alert';

let instanceCounter = 0;

/**
 * Ax, the Ax Tracker mascot: an axolotl with round glasses and a bow tie.
 * Pure SVG + CSS animations; the mood input switches between the animated states.
 */
@Component({
  selector: 'app-ax',
  templateUrl: './ax-mascot.html',
  styleUrl: './ax-mascot.scss'
})
export class AxMascot {
  readonly mood = input<AxMood>('idle');
  readonly size = input<number>(160);

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

  private readonly i18n = inject(I18n);

  readonly label = computed(() => {
    const labels: Record<AxMood, string> = {
      idle: 'Ax, mascota Ax Tracker',
      thinking: 'Ax se gandeste',
      nodding: 'Ax raspunde',
      happy: 'Ax este fericit',
      worried: 'Ax este ingrijorat',
      alert: 'Ax are ceva important de spus'
    };
    return this.i18n.t(labels[this.mood()]);
  });
}
