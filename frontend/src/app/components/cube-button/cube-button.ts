import { Component, input } from '@angular/core';

/**
 * Link button with a 3D cube that rolls over on hover (style adapted from Uiverse.io by aguerquin).
 * `label` is on the front face, `hoverLabel` on the face that rolls into view.
 */
@Component({
  selector: 'app-cube-button',
  templateUrl: './cube-button.html',
  styleUrl: './cube-button.scss'
})
export class CubeButton {
  readonly href = input.required<string>();
  readonly label = input.required<string>();
  readonly hoverLabel = input.required<string>();
  readonly icon = input<'code' | 'briefcase'>('code');
}
