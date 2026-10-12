import { gsap } from 'gsap';
import { ScrollTrigger } from 'gsap/ScrollTrigger';

gsap.registerPlugin(ScrollTrigger);

export { gsap, ScrollTrigger };

/** The signature ease of the app: fast start, long soft landing (same feel as the About page). */
export const CINEMATIC_EASE = 'expo.out';

/** True when the user asked the OS for less motion: then every animation is skipped. */
export function reducedMotion(): boolean {
  return typeof window === 'undefined'
    || window.matchMedia?.('(prefers-reduced-motion: reduce)').matches === true;
}

/** Mouse or trackpad (not touch): hover effects only make sense there. */
export function finePointer(): boolean {
  return typeof window !== 'undefined'
    && window.matchMedia?.('(hover: hover) and (pointer: fine)').matches === true;
}
