import { AfterViewInit, Component, computed, effect, ElementRef, inject, OnDestroy, signal, untracked } from '@angular/core';
import { gsap } from 'gsap';
import { ScrollTrigger } from 'gsap/ScrollTrigger';

import { AxMascot, AxMood } from '../../components/ax-mascot/ax-mascot';
import { CubeButton } from '../../components/cube-button/cube-button';
import { I18n } from '../../i18n/i18n.service';
import { TranslatePipe } from '../../i18n/translate.pipe';

gsap.registerPlugin(ScrollTrigger);

interface Project {
  name: string;
  description: string;
  stack: string[];
  url: string;
  kind: 'web' | 'hardware' | 'java' | 'data';
}

interface Milestone {
  period: string;
  title: string;
  detail: string;
}

/** One word of a sentence revealed on scroll; `em` words get the accent colour. */
interface Word {
  text: string;
  em: boolean;
}

interface TextPart {
  key: string;
  em?: boolean;
}

/**
 * The About page tells the story as you scroll (GSAP + ScrollTrigger):
 * the name rises letter by letter, sentences light up word by word, the timeline draws itself
 * and the projects slide sideways. Without animations (reduced motion, small screens) the same
 * content is simply shown, because every tween starts *from* a hidden state.
 */
@Component({
  selector: 'app-about',
  imports: [AxMascot, CubeButton, TranslatePipe],
  templateUrl: './about.html',
  styleUrl: './about.scss'
})
export class About implements AfterViewInit, OnDestroy {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly i18n = inject(I18n);

  private media?: gsap.MatchMedia;
  private viewReady = false;
  private setupTimer?: ReturnType<typeof setTimeout>;

  readonly year = new Date().getFullYear();

  /** Ax, the guide on the side: his mood and what he says change with each chapter. */
  readonly guideMood = signal<AxMood>('idle');
  readonly guideLine = signal('');

  readonly githubUrl = 'https://github.com/bogdansusman-prog';
  readonly linkedinUrl = 'https://www.linkedin.com/in/bogdan-susman/';

  /** "Bogdan Șușman" as single letters (spaces kept as non-breaking spaces). */
  readonly nameLetters = Array.from('Bogdan Șușman').map(letter => (letter === ' ' ? '\u00a0' : letter));

  readonly mottoWords = computed(() => this.words([{ key: '„Învăț construind: de la tranzistor până la aplicație.”' }]));

  readonly leadWords = computed(() => this.words([
    { key: 'Student la Calculatoare la Universitatea Tehnică din Cluj-Napoca și full-stack developer din Oradea. Scriu aplicații web și mobile cu Angular, Flutter și ASP.NET Core, iar când codul nu-mi ajunge, cobor în hardware și proiectez procesoare în VHDL.' }
  ]));

  readonly whyWords = computed(() => this.words([
    { key: 'Ca student, banii se duc repede și pe lucruri mărunte: o cafea aici, un Bolt acolo, un abonament uitat. Voiam o aplicație care să nu-mi arate doar' },
    { key: 'cât', em: true },
    { key: 'am cheltuit, ci și' },
    { key: 'de ce', em: true },
    { key: ': ce am regretat, ce s-a scumpit fără să observ și unde voi fi la sfârșitul lunii.' }
  ]));

  readonly reasonWords = computed(() => this.words([
    { key: 'Dar motivul mai mare a fost să mă testez. Am vrut un proiect pe care să-l construiesc cap-coadă, singur: bază de date, API, securitate, interfață, algoritmi, teste. Să iau decizii ca într-un produs real și să învăț din ele. A pornit ca un simplu tabel cu tranzacții și a crescut pas cu pas: statistici, prognoze Monte Carlo, detectarea abonamentelor, conturi separate și, la final, Ax.' }
  ]));

  readonly finaleWords = computed(() => this.words([
    { key: 'Ax Tracker e felul meu de a arăta ce pot face și, sper, pasul care mă duce mai departe, spre primul meu internship și spre o carieră în software.' }
  ]));

  constructor() {
    // New language = new word spans: rebuild the animations once Angular has rendered them.
    effect(() => {
      this.i18n.language();
      untracked(() => {
        if (this.viewReady) {
          this.scheduleSetup();
        }
      });
    });
  }

  ngAfterViewInit(): void {
    this.viewReady = true;
    this.scheduleSetup();
  }

  ngOnDestroy(): void {
    clearTimeout(this.setupTimer);
    this.media?.revert();
  }

  /** Images change the page height; ScrollTrigger must re-measure its start/end points. */
  refresh(): void {
    ScrollTrigger.refresh();
  }

  readonly stack = [
    'C#', 'ASP.NET Core', 'Entity Framework Core', 'PostgreSQL', 'ASP.NET Identity + JWT',
    'Angular', 'TypeScript', 'Signals', 'Chart.js', 'SCSS', 'SVG + CSS animations', 'xUnit'
  ];

  readonly highlights = [
    { title: 'Conturi separate', text: 'Fiecare utilizator își vede doar datele lui, cu filtre globale în EF Core și autentificare JWT.' },
    { title: 'A meritat?', text: 'Notezi cheltuielile după 7 zile și afli unde îți pierzi, de fapt, banii.' },
    { title: 'Detectiv de abonamente', text: 'Găsește singur plățile recurente și te anunță când ceva s-a scumpit pe ascuns.' },
    { title: 'Prognoza Monte Carlo', text: '2.000 de simulări pe istoricul tău real arată unde va fi soldul peste 30–90 de zile.' },
    { title: 'Adăugare în limbaj natural', text: '„ieri 45 lei pizza cu Andrei” devine o tranzacție completă, offline.' },
    { title: 'Ax, consilierul', text: 'Un scor de sănătate financiară explicabil, cu 3 pași concreți și un chat care răspunde cu cifrele tale.' }
  ];

  readonly projects: Project[] = [
    {
      name: 'OwlBank',
      description: 'Aplicație de digital banking făcută în echipă: frontend complet în Angular (autentificare JWT, transferuri, carduri, extrase) și o versiune mobilă în Flutter.',
      stack: ['Angular', 'TypeScript', 'Angular Material', 'Flutter', 'Dart'],
      url: 'https://github.com/bogdansusman-prog/OwlBank-Fe',
      kind: 'web'
    },
    {
      name: 'MIPS32 Pipeline',
      description: 'Procesor MIPS pe 32 de biți cu pipeline în 5 etaje, scris în VHDL și rulat pe placa FPGA Nexys A7.',
      stack: ['VHDL', 'Vivado', 'FPGA'],
      url: 'https://github.com/bogdansusman-prog/mips32-pipeline-vhdl',
      kind: 'hardware'
    },
    {
      name: 'FIFO pe FPGA',
      description: 'Memorie FIFO 16×16 cu pointeri circulari, afișaj pe 7 segmente și butoane debounced, pe placa Basys 3. Are și demo video.',
      stack: ['VHDL', 'Basys 3', 'Digital design'],
      url: 'https://github.com/bogdansusman-prog/fifo-buffer-vhdl-basys3',
      kind: 'hardware'
    },
    {
      name: 'Queue Simulator',
      description: 'Simulator de cozi multithreaded: fiecare casă rulează pe propriul thread, cu strategii de distribuire și animație în timp real.',
      stack: ['Java', 'Multithreading', 'Swing'],
      url: 'https://github.com/bogdansusman-prog/queue-simulator-java',
      kind: 'java'
    },
    {
      name: 'Orders Management',
      description: 'Gestiune de comenzi pe PostgreSQL, cu arhitectură stratificată și un DAO generic construit cu reflection.',
      stack: ['Java', 'PostgreSQL', 'JDBC', 'Reflection'],
      url: 'https://github.com/bogdansusman-prog/orders-management-java',
      kind: 'java'
    },
    {
      name: 'Library Database',
      description: 'Bază de date pentru o bibliotecă: schemă normalizată cu relații 1:1 și M:N și 16 interogări SQL.',
      stack: ['PostgreSQL', 'SQL', 'Database design'],
      url: 'https://github.com/bogdansusman-prog/library-database-postgresql',
      kind: 'data'
    }
  ];

  readonly milestones: Milestone[] = [
    {
      period: '2020 – 2024',
      title: 'Liceul Teoretic „Aurel Lazăr”, Oradea',
      detail: 'Profil matematică-informatică. Primele programe, prima pasiune pentru logică și un proiect Erasmus+ în Portugalia.'
    },
    {
      period: '2024 – prezent',
      title: 'Universitatea Tehnică din Cluj-Napoca',
      detail: 'Calculatoare și Tehnologia Informației: de la Java, OOP și baze de date până la procesoare MIPS și FPGA.'
    },
    {
      period: 'Vara 2026',
      title: 'Full-stack pe cont propriu',
      detail: 'Angular, C#, ASP.NET Core, Flutter. Am construit OwlBank în echipă și apoi Ax Tracker de la zero.'
    },
    {
      period: 'Următorul pas',
      title: 'Primul internship',
      detail: 'Caut un loc unde să contribui la produse reale și să cresc ca inginer software. Ax Tracker e dovada că pot duce un proiect de la idee la produs.'
    }
  ];

  private words(parts: TextPart[]): Word[] {
    const result: Word[] = [];

    for (const part of parts) {
      for (const token of this.i18n.t(part.key).split(/\s+/).filter(Boolean)) {
        // ": ce am regretat" continues the previous word instead of starting with a lonely ":".
        if (/^[,.:;!?”]/.test(token) && result.length > 0) {
          result[result.length - 1] = { ...result[result.length - 1], text: result[result.length - 1].text + token[0] };
          if (token.length > 1) {
            result.push({ text: token.slice(1), em: !!part.em });
          }
          continue;
        }
        result.push({ text: token, em: !!part.em });
      }
    }

    return result;
  }

  private scheduleSetup(): void {
    clearTimeout(this.setupTimer);
    this.setupTimer = setTimeout(() => this.setup(), 0);
  }

  private setup(): void {
    this.media?.revert();
    const root = this.host.nativeElement;
    const q = gsap.utils.selector(root);

    this.media = gsap.matchMedia();

    this.media.add(
      {
        motion: '(prefers-reduced-motion: no-preference)',
        wide: '(min-width: 900px)',
        roomy: '(min-width: 1200px)',
        finePointer: '(pointer: fine)'
      },
      (context: gsap.Context) => {
        const { motion, wide, roomy, finePointer } = context.conditions as {
          motion: boolean; wide: boolean; roomy: boolean; finePointer: boolean;
        };
        if (!motion) {
          return;
        }

        const cleanups: Array<() => void> = [];

        this.animateIntro(q);
        this.animateAmbient(q, root, cleanups);
        if (finePointer) {
          this.animateCursor(q, root, cleanups);
        }
        if (roomy) {
          // Ax needs a free margin next to the text, so he only joins on large screens.
          this.animateGuide(q, cleanups);
        }
        this.animateChapters(q, wide);

        return () => cleanups.forEach(cleanup => cleanup());
      }
    );

    ScrollTrigger.refresh();
  }

  /** The name decodes from random letters, then you "fly through" it into the story. */
  private animateIntro(q: (selector: string) => Element[]): void {
    const glyphs = 'ABCDEFGHIJKLMNOPRSTUVXZĂÎȘȚ01#%&*';
    const letters = q('.intro .letter') as HTMLElement[];

    const intro = gsap.timeline({ defaults: { ease: 'power4.out' } });
    intro
      .from(q('.intro .eyebrow'), { y: 20, opacity: 0, duration: 0.8 })
      .from(q('.intro .hello'), { y: 14, opacity: 0, duration: 0.7 }, '-=0.4')
      .from(letters, { yPercent: 110, duration: 0.9, stagger: 0.04 }, '-=0.3');

    // Scramble: every letter flickers through random glyphs before landing on the real one.
    letters.forEach((letter, index) => {
      const final = letter.textContent ?? '';
      if (!final.trim()) {
        return;
      }
      const state = { progress: 0 };
      intro.to(state, {
        progress: 1,
        duration: 0.9,
        ease: 'none',
        onUpdate: () => {
          letter.textContent = state.progress < 1 ? glyphs[Math.floor(Math.random() * glyphs.length)] : final;
        },
        onComplete: () => {
          letter.textContent = final;
        }
      }, 0.9 + index * 0.06);
    });

    intro
      .from(q('.intro .pronounce, .intro .scroll-hint'), { y: 16, opacity: 0, duration: 0.8, stagger: 0.15 }, '-=0.4')
      .from(q('.intro .logo'), { scale: 0.5, opacity: 0, rotate: -20, duration: 1.3, ease: 'elastic.out(1, 0.55)' }, 0.4);

    // Zoom-through: the intro stays pinned while the name grows huge and dissolves.
    gsap.timeline({
      scrollTrigger: { trigger: q('.intro')[0], start: 'top top', end: '+=130%', pin: true, scrub: 0.8 }
    })
      .to(q('.intro .eyebrow, .intro .hello, .intro .pronounce, .intro .scroll-hint'), { opacity: 0, y: -40, duration: 0.3 }, 0)
      .to(q('.intro .logo'), { y: -120, scale: 0.4, opacity: 0, duration: 0.4 }, 0)
      .to(q('.intro .letters'), { scale: 11, ease: 'power2.in', duration: 1 }, 0)
      .to(q('.intro .letters'), { opacity: 0, duration: 0.25 }, 0.75);
  }

  /** Soft light blobs drift on their own, one follows the mouse, and the colours shift with the scroll. */
  private animateAmbient(q: (selector: string) => Element[], root: HTMLElement, cleanups: Array<() => void>): void {
    for (const blob of q('.ambient .drift')) {
      gsap.to(blob, {
        x: 'random(-220, 220)',
        y: 'random(-160, 160)',
        scale: 'random(0.8, 1.3)',
        duration: 'random(7, 13)',
        ease: 'sine.inOut',
        repeat: -1,
        yoyo: true,
        repeatRefresh: true
      });
    }

    gsap.to(q('.ambient')[0], {
      filter: 'hue-rotate(70deg)',
      ease: 'none',
      scrollTrigger: { trigger: root, start: 'top top', end: 'bottom bottom', scrub: true }
    });

    const follower = q('.ambient .follow')[0];
    if (follower) {
      const moveX = gsap.quickTo(follower, 'x', { duration: 1.6, ease: 'power3.out' });
      const moveY = gsap.quickTo(follower, 'y', { duration: 1.6, ease: 'power3.out' });
      const onMove = (event: PointerEvent) => {
        moveX(event.clientX - window.innerWidth / 2);
        moveY(event.clientY - window.innerHeight / 2);
      };
      window.addEventListener('pointermove', onMove);
      cleanups.push(() => window.removeEventListener('pointermove', onMove));
    }
  }

  /** A small dot plus a lazy ring replace the cursor; the ring grows over anything clickable. */
  private animateCursor(q: (selector: string) => Element[], root: HTMLElement, cleanups: Array<() => void>): void {
    const dot = q('.cursor-dot')[0];
    const ring = q('.cursor-ring')[0];
    if (!dot || !ring) {
      return;
    }

    root.classList.add('custom-cursor');
    const dotX = gsap.quickTo(dot, 'x', { duration: 0.08, ease: 'power3' });
    const dotY = gsap.quickTo(dot, 'y', { duration: 0.08, ease: 'power3' });
    const ringX = gsap.quickTo(ring, 'x', { duration: 0.45, ease: 'power3' });
    const ringY = gsap.quickTo(ring, 'y', { duration: 0.45, ease: 'power3' });

    const onMove = (event: PointerEvent) => {
      const inside = root.contains(event.target as Node);
      gsap.to([dot, ring], { autoAlpha: inside ? 1 : 0, duration: 0.2, overwrite: 'auto' });
      dotX(event.clientX);
      dotY(event.clientY);
      ringX(event.clientX);
      ringY(event.clientY);

      const clickable = !!(event.target as Element | null)?.closest?.('a, button');
      gsap.to(ring, { scale: clickable ? 2.2 : 1, duration: 0.3, overwrite: 'auto' });
    };

    const onLeave = () => gsap.to([dot, ring], { autoAlpha: 0, duration: 0.2 });

    window.addEventListener('pointermove', onMove);
    document.addEventListener('pointerleave', onLeave);
    cleanups.push(() => {
      window.removeEventListener('pointermove', onMove);
      document.removeEventListener('pointerleave', onLeave);
      root.classList.remove('custom-cursor');
    });
  }

  /** Ax swims down the side of the page and reacts to each chapter. */
  private animateGuide(q: (selector: string) => Element[], cleanups: Array<() => void>): void {
    const guide = q('.guide')[0];
    if (!guide) {
      return;
    }

    const root = this.host.nativeElement;
    root.classList.add('with-guide');
    cleanups.push(() => root.classList.remove('with-guide'));

    // Appears once the intro is gone, then drifts down while you read.
    gsap.fromTo(guide, { autoAlpha: 0, x: 120 }, {
      autoAlpha: 1, x: 0, duration: 1, ease: 'back.out(1.4)',
      scrollTrigger: { trigger: q('.motto')[0], start: 'top 85%', toggleActions: 'play none none reverse' }
    });

    gsap.fromTo(guide, { y: 0 }, {
      y: () => window.innerHeight * 0.42,
      ease: 'none',
      scrollTrigger: { trigger: this.host.nativeElement, start: 'top top', end: 'bottom bottom', scrub: 1.2, invalidateOnRefresh: true }
    });

    // A gentle swim: side to side with a little tilt.
    gsap.to(q('.guide .swim')[0], { x: -18, rotate: -6, duration: 2.4, ease: 'sine.inOut', repeat: -1, yoyo: true });

    for (const chapter of q('[data-mood]') as HTMLElement[]) {
      ScrollTrigger.create({
        trigger: chapter,
        // The chapter crossing the middle of the screen is the one Ax talks about (ranges never overlap).
        start: 'top center',
        end: 'bottom center',
        onToggle: (self: ScrollTrigger) => {
          if (self.isActive) {
            this.guideMood.set((chapter.dataset['mood'] as AxMood) ?? 'idle');
            this.guideLine.set(chapter.dataset['line'] ?? '');
            gsap.fromTo(q('.guide .bubble'), { scale: 0.6, opacity: 0 }, { scale: 1, opacity: 1, duration: 0.45, ease: 'back.out(2)' });
          }
        }
      });
    }
  }

  private animateChapters(q: (selector: string) => Element[], wide: boolean): void {
    // Sentences light up word by word, in step with the scroll.
    for (const block of q('.reveal')) {
      const pinned = block.classList.contains('pin');
      gsap.fromTo(
        block.querySelectorAll('.w'),
        { opacity: 0.12, filter: 'blur(4px)' },
        {
          opacity: 1,
          filter: 'blur(0px)',
          stagger: 0.1,
          ease: 'none',
          scrollTrigger: pinned && wide
            ? { trigger: block, start: 'center center', end: '+=120%', pin: true, scrub: 0.6 }
            : { trigger: block, start: 'top 80%', end: 'bottom 45%', scrub: 0.6 }
        }
      );
    }

    for (const label of q('.chapter-label')) {
      gsap.from(label, {
        x: -30, opacity: 0, duration: 0.9, ease: 'power3.out',
        scrollTrigger: { trigger: label, start: 'top 85%' }
      });
    }

    ScrollTrigger.batch(q('.feature'), {
      start: 'top 88%',
      once: true,
      onEnter: (batch: Element[]) => gsap.from(batch, { y: 50, opacity: 0, duration: 0.9, stagger: 0.12, ease: 'power3.out' })
    });

    gsap.fromTo(q('.marquee .row-a'), { xPercent: 0 }, {
      xPercent: -25, ease: 'none',
      scrollTrigger: { trigger: q('.marquee')[0], start: 'top bottom', end: 'bottom top', scrub: true }
    });
    gsap.fromTo(q('.marquee .row-b'), { xPercent: -25 }, {
      xPercent: 0, ease: 'none',
      scrollTrigger: { trigger: q('.marquee')[0], start: 'top bottom', end: 'bottom top', scrub: true }
    });

    gsap.from(q('.meet .ax-wrap'), {
      x: -120, rotate: -12, opacity: 0, duration: 1.2, ease: 'power3.out',
      scrollTrigger: { trigger: q('.meet')[0], start: 'top 75%' }
    });
    gsap.from(q('.meet .meet-text > *'), {
      y: 30, opacity: 0, duration: 0.9, stagger: 0.15, ease: 'power3.out',
      scrollTrigger: { trigger: q('.meet')[0], start: 'top 70%' }
    });

    gsap.fromTo(q('.journey .line-fill'), { scaleY: 0 }, {
      scaleY: 1, ease: 'none',
      scrollTrigger: { trigger: q('.journey .steps')[0], start: 'top 70%', end: 'bottom 60%', scrub: true }
    });
    for (const step of q('.journey .step')) {
      gsap.from(step, {
        x: 40, opacity: 0, duration: 0.8, ease: 'power3.out',
        scrollTrigger: { trigger: step, start: 'top 72%' }
      });
    }

    const viewport = q('.projects-viewport')[0];
    const track = q('.projects-track')[0];
    if (wide && viewport && track) {
      gsap.set(viewport, { overflow: 'hidden' });
      const distance = () => Math.max(0, track.scrollWidth - viewport.clientWidth);
      gsap.to(track, {
        x: () => -distance(),
        ease: 'none',
        scrollTrigger: {
          trigger: q('.projects')[0],
          start: 'top top',
          end: () => `+=${distance()}`,
          pin: true,
          scrub: 0.8,
          invalidateOnRefresh: true
        }
      });
    } else {
      ScrollTrigger.batch(q('.project'), {
        start: 'top 90%',
        once: true,
        onEnter: (batch: Element[]) => gsap.from(batch, { y: 40, opacity: 0, duration: 0.8, stagger: 0.1, ease: 'power3.out' })
      });
    }

    gsap.from(q('.finale .actions > *'), {
      y: 24, opacity: 0, duration: 0.8, stagger: 0.12, ease: 'back.out(1.6)',
      scrollTrigger: { trigger: q('.finale .actions')[0], start: 'top 90%' }
    });
  }
}
