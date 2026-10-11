import { AfterViewInit, Component, computed, effect, ElementRef, inject, OnDestroy, untracked } from '@angular/core';
import { gsap } from 'gsap';
import { ScrollTrigger } from 'gsap/ScrollTrigger';

import { AxMascot } from '../../components/ax-mascot/ax-mascot';
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
        wide: '(min-width: 900px)'
      },
      (context: gsap.Context) => {
        const { motion, wide } = context.conditions as { motion: boolean; wide: boolean };
        if (!motion) {
          return;
        }

        // 1. Intro: the name rises letter by letter, then the rest fades in.
        const intro = gsap.timeline({ defaults: { ease: 'power4.out' } });
        intro
          .from(q('.intro .eyebrow'), { y: 20, opacity: 0, duration: 0.8 })
          .from(q('.intro .letter'), { yPercent: 110, duration: 1.1, stagger: 0.045 }, '-=0.4')
          .from(q('.intro .pronounce, .intro .scroll-hint'), { y: 16, opacity: 0, duration: 0.8, stagger: 0.15 }, '-=0.6')
          .from(q('.intro .logo'), { scale: 0.6, opacity: 0, duration: 1.2, ease: 'elastic.out(1, 0.6)' }, '-=1');

        // ...and leaves softly while you scroll away.
        gsap.to(q('.intro .intro-inner'), {
          yPercent: -18,
          opacity: 0.15,
          scale: 0.94,
          ease: 'none',
          scrollTrigger: { trigger: q('.intro')[0], start: 'top top', end: 'bottom top', scrub: true }
        });

        // 2. Sentences light up word by word, in step with the scroll.
        for (const block of q('.reveal')) {
          const pinned = block.classList.contains('pin');
          gsap.fromTo(
            block.querySelectorAll('.w'),
            { opacity: 0.12 },
            {
              opacity: 1,
              stagger: 0.1,
              ease: 'none',
              scrollTrigger: pinned && wide
                ? { trigger: block, start: 'center center', end: '+=120%', pin: true, scrub: 0.6 }
                : { trigger: block, start: 'top 80%', end: 'bottom 45%', scrub: 0.6 }
            }
          );
        }

        // Chapter labels slide in with a line.
        for (const label of q('.chapter-label')) {
          gsap.from(label, {
            x: -30,
            opacity: 0,
            duration: 0.9,
            ease: 'power3.out',
            scrollTrigger: { trigger: label, start: 'top 85%' }
          });
        }

        // 3. Features arrive one after another.
        ScrollTrigger.batch(q('.feature'), {
          start: 'top 88%',
          once: true,
          onEnter: (batch: Element[]) => gsap.from(batch, { y: 50, opacity: 0, duration: 0.9, stagger: 0.12, ease: 'power3.out' })
        });

        // 4. The stack drifts sideways in two opposite rows.
        gsap.fromTo(q('.marquee .row-a'), { xPercent: 0 }, {
          xPercent: -25, ease: 'none',
          scrollTrigger: { trigger: q('.marquee')[0], start: 'top bottom', end: 'bottom top', scrub: true }
        });
        gsap.fromTo(q('.marquee .row-b'), { xPercent: -25 }, {
          xPercent: 0, ease: 'none',
          scrollTrigger: { trigger: q('.marquee')[0], start: 'top bottom', end: 'bottom top', scrub: true }
        });

        // 5. Ax swims in.
        gsap.from(q('.meet .ax-wrap'), {
          x: -120, rotate: -12, opacity: 0, duration: 1.2, ease: 'power3.out',
          scrollTrigger: { trigger: q('.meet')[0], start: 'top 75%' }
        });
        gsap.from(q('.meet .meet-text > *'), {
          y: 30, opacity: 0, duration: 0.9, stagger: 0.15, ease: 'power3.out',
          scrollTrigger: { trigger: q('.meet')[0], start: 'top 70%' }
        });

        // 6. The journey line draws itself; each milestone appears when the line reaches it.
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

        // 7. Projects: a pinned horizontal gallery on wide screens.
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

        // 8. Finale: the buttons pop in after the last sentence.
        gsap.from(q('.finale .actions > *'), {
          y: 24, opacity: 0, duration: 0.8, stagger: 0.12, ease: 'back.out(1.6)',
          scrollTrigger: { trigger: q('.finale .actions')[0], start: 'top 90%' }
        });
      }
    );

    ScrollTrigger.refresh();
  }
}
