import { Component, computed, effect, inject, OnDestroy, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter, map } from 'rxjs';

import { AxMascot, AxMood } from '../ax-mascot/ax-mascot';

interface PageTip {
  mood: AxMood;
  tips: string[];
}

const STORAGE_KEY = 'ax-tracker.companion-collapsed';

/** Pages where Ax gives a tip, keyed by the first URL segment. */
const TIPS: Record<string, PageTip> = {
  dashboard: {
    mood: 'idle',
    tips: [
      'Scrie „ieri 45 lei pizza” in caseta de sus si completez eu restul.',
      'Cu cat adaugi mai des tranzactiile, cu atat sfaturile mele sunt mai bune.',
      'Ai un minut? Intreaba-ma cum stai luna asta.'
    ]
  },
  transactions: {
    mood: 'idle',
    tips: [
      'Poti filtra dupa categorie, tip si perioada.',
      'Bifeaza mai multe tranzactii ca sa le stergi deodata.',
      'O descriere clara ma ajuta sa gasesc abonamentele ascunse.'
    ]
  },
  categories: {
    mood: 'idle',
    tips: [
      'Categorii clare inseamna sfaturi mai bune de la mine.',
      'Nu poti sterge o categorie care inca are tranzactii, ca sa nu pierzi date.'
    ]
  },
  comparator: {
    mood: 'thinking',
    tips: [
      'Uita-te la luna cu cele mai mari cheltuieli: acolo e cel mai mult de castigat.',
      'Daca o categorie creste luna de luna, intreaba-ma de ea.'
    ]
  },
  insights: {
    mood: 'alert',
    tips: [
      'Noteaza cheltuielile la „A meritat?” ca sa invat ce regreti.',
      'Verifica abonamentele: unele se scumpesc pe ascuns!',
      'Banda din prognoza iti arata cat de sigur sunt pe soldul de la final de luna.'
    ]
  },
  settings: {
    mood: 'idle',
    tips: [
      'Seteaza venitul pe ora si iti arat preturile in ore de munca.',
      'Poti alege daca iti raspund in romana sau in engleza.'
    ]
  },
  profile: {
    mood: 'happy',
    tips: [
      'Pune-ti o poza de profil, sa stiu cu cine vorbesc!',
      'O parola lunga e mai sigura decat una complicata.'
    ]
  },
  about: {
    mood: 'happy',
    tips: [
      'El e omul care m-a desenat!',
      'Stiai ca axolotlii isi pot regenera aproape orice parte a corpului?'
    ]
  }
};

/**
 * Ax lives in the bottom-right corner of every page and gives a tip for the current page.
 * Hidden on the advisor page (where Ax is already the star) and on login / register.
 */
@Component({
  selector: 'app-ax-companion',
  imports: [AxMascot, RouterLink],
  templateUrl: './ax-companion.html',
  styleUrl: './ax-companion.scss'
})
export class AxCompanion implements OnDestroy {
  private readonly router = inject(Router);

  private readonly section = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map(event => AxCompanion.sectionOf(event.urlAfterRedirects))
    ),
    { initialValue: AxCompanion.sectionOf(this.router.url) }
  );

  readonly collapsed = signal(AxCompanion.readCollapsed());
  readonly tipIndex = signal(0);

  readonly page = computed(() => TIPS[this.section()] ?? null);

  readonly tip = computed(() => {
    const page = this.page();
    return page ? page.tips[this.tipIndex() % page.tips.length] : '';
  });

  readonly mood = computed<AxMood>(() => this.page()?.mood ?? 'idle');

  /** The speech bubble shows up on every page change and hides itself after a few seconds. */
  readonly bubbleOpen = signal(true);
  private hideTimer?: ReturnType<typeof setTimeout>;

  constructor() {
    effect(() => {
      this.section();
      this.showBubble();
    });
  }

  ngOnDestroy(): void {
    clearTimeout(this.hideTimer);
  }

  nextTip(): void {
    this.tipIndex.update(index => index + 1);
    this.showBubble();
  }

  onAxClick(): void {
    if (this.collapsed()) {
      this.toggle();
      this.showBubble();
    } else if (!this.bubbleOpen()) {
      this.showBubble();
    } else {
      this.nextTip();
    }
  }

  showBubble(): void {
    this.bubbleOpen.set(true);
    clearTimeout(this.hideTimer);
    this.hideTimer = setTimeout(() => this.bubbleOpen.set(false), 9000);
  }

  toggle(): void {
    const value = !this.collapsed();
    this.collapsed.set(value);

    try {
      localStorage.setItem(STORAGE_KEY, value ? '1' : '0');
    } catch {
      // storage unavailable: the choice lasts until reload
    }
  }

  private static sectionOf(url: string): string {
    return url.split('?')[0].split('#')[0].split('/').filter(Boolean)[0] ?? 'dashboard';
  }

  private static readCollapsed(): boolean {
    try {
      return localStorage.getItem(STORAGE_KEY) === '1';
    } catch {
      return false;
    }
  }
}
