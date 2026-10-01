import { Component, computed, ElementRef, inject, OnDestroy, OnInit, signal, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';

import { AdvisorReport, AppLanguage, ChatTurn } from '../../models/insights.model';
import { InsightsApiService } from '../../services/insights-api.service';
import { SettingsStore } from '../../services/settings.store';
import { formatMoney } from '../../shared/format';
import { AxMascot, AxMood } from '../../components/ax-mascot/ax-mascot';

/**
 * "Ax": a rule-based financial advisor with an animated axolotl mascot. Everything is
 * calculated locally from the user's data (no external AI): score with breakdown,
 * verdict, actions and a keyword chat. Ax thinks while it computes, nods while it answers.
 */
@Component({
  selector: 'app-advisor',
  imports: [FormsModule, AxMascot],
  templateUrl: './advisor.html',
  styleUrl: './advisor.scss'
})
export class Advisor implements OnInit, OnDestroy {
  private readonly api = inject(InsightsApiService);
  readonly settings = inject(SettingsStore);

  @ViewChild('chatLog')
  private chatLog?: ElementRef<HTMLDivElement>;

  readonly report = signal<AdvisorReport | null>(null);
  readonly loadingReport = signal(false);
  readonly reportError = signal('');

  readonly messages = signal<ChatTurn[]>([]);
  readonly question = signal('');
  readonly sending = signal(false);
  readonly chatError = signal('');

  readonly formatMoney = formatMoney;

  /** Index of the assistant message currently being "typed" and how much of it is visible. */
  readonly typingIndex = signal<number | null>(null);
  readonly typedText = signal('');
  private typingTimer?: ReturnType<typeof setInterval>;
  private nodTimeout?: ReturnType<typeof setTimeout>;

  /** Ax's mood follows what is happening on the page. */
  readonly mood = computed<AxMood>(() => {
    if (this.sending() || this.loadingReport()) {
      return 'thinking';
    }
    if (this.typingIndex() !== null) {
      return 'nodding';
    }
    if (this.chatError() || this.reportError()) {
      return 'worried';
    }

    const report = this.report();
    if (!report) {
      return 'idle';
    }
    if (report.verdict === 'good') {
      return 'happy';
    }
    return report.verdict === 'bad' ? 'worried' : 'idle';
  });

  readonly greeting = computed(() => {
    const report = this.report();
    const ro = this.language() === 'ro';

    if (!report) {
      return ro ? 'Salut! Sunt Ax. Stai putin sa ma uit pe banii tai...' : 'Hi! I am Ax. Give me a second to look at your money...';
    }
    if (report.verdict === 'good') {
      return ro ? `Scor ${report.score}/100! Stai foarte bine. Intreaba-ma orice.` : `Score ${report.score}/100! You're doing great. Ask me anything.`;
    }
    if (report.verdict === 'bad') {
      return ro ? `Scor ${report.score}/100... Hai sa vedem impreuna ce putem repara.` : `Score ${report.score}/100... Let's fix this together.`;
    }
    return ro ? `Scor ${report.score}/100. Merge, dar am cateva idei pentru tine.` : `Score ${report.score}/100. Not bad, but I have a few ideas for you.`;
  });

  readonly suggestions: Record<AppLanguage, string[]> = {
    ro: [
      'Cum stau?',
      'Unde pot sa economisesc?',
      'Ce abonamente am?',
      'Cati bani o sa am la sfarsitul lunii?',
      'Cum stau fata de luna trecuta?',
      'Cate ore de munca m-au costat cheltuielile?'
    ],
    en: [
      'How am I doing?',
      'Where can I save?',
      'What subscriptions do I have?',
      'How much will I have at the end of the month?',
      'Am I doing better than last month?',
      'How many work hours did my spending cost?'
    ]
  };

  ngOnInit(): void {
    this.settings.load();
    this.loadReport();
  }

  ngOnDestroy(): void {
    clearInterval(this.typingTimer);
    clearTimeout(this.nodTimeout);
  }

  language(): AppLanguage {
    return this.settings.language();
  }

  setLanguage(language: AppLanguage): void {
    if (language === this.language()) {
      return;
    }

    this.settings
      .save({ hourlyRate: this.settings.hourlyRate(), language })
      .subscribe(() => this.loadReport());
  }

  loadReport(): void {
    this.loadingReport.set(true);
    this.reportError.set('');

    this.api.getAdvisorReport(this.language()).subscribe({
      next: report => {
        this.report.set(report);
        this.loadingReport.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.reportError.set(this.describeError(error));
        this.loadingReport.set(false);
      }
    });
  }

  ask(text?: string): void {
    const content = (text ?? this.question()).trim();

    if (!content || this.sending()) {
      return;
    }

    const history: ChatTurn[] = [...this.messages(), { role: 'user', content }];
    this.messages.set(history);
    this.question.set('');
    this.sending.set(true);
    this.chatError.set('');
    this.scrollChat();

    this.api.chat(history, this.language()).subscribe({
      next: response => {
        this.messages.update(list => [...list, { role: 'assistant', content: response.reply }]);
        this.sending.set(false);
        this.typeOut(this.messages().length - 1, response.reply);
      },
      error: (error: HttpErrorResponse) => {
        this.chatError.set(this.describeError(error));
        this.sending.set(false);
      }
    });
  }

  scoreColor(score: number): string {
    const hue = Math.round((Math.max(0, Math.min(100, score)) / 100) * 120);
    return `hsl(${hue} 70% 45%)`;
  }

  componentPercent(points: number, max: number): number {
    return max > 0 ? Math.round((points / max) * 100) : 0;
  }

  verdictLabel(verdict: string): string {
    const labels: Record<string, Record<AppLanguage, string>> = {
      good: { ro: 'Esti pe drumul bun', en: 'You are on track' },
      ok: { ro: 'Merge, dar se poate mai bine', en: 'Okay, but could be better' },
      bad: { ro: 'Atentie, esti pe minus', en: 'Warning: you are off track' }
    };
    return (labels[verdict] ?? labels['ok'])[this.language()];
  }

  private describeError(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'API-ul nu raspunde. Porneste backend-ul (dotnet run --launch-profile https).';
    }
    return 'A aparut o eroare. Incearca din nou.';
  }

  /** Reveals Ax's answer a few characters at a time while Ax nods. */
  private typeOut(index: number, text: string): void {
    clearInterval(this.typingTimer);
    clearTimeout(this.nodTimeout);

    const reduceMotion = typeof window !== 'undefined'
      && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;

    if (reduceMotion) {
      this.typingIndex.set(null);
      this.scrollChat();
      return;
    }

    this.typingIndex.set(index);
    this.typedText.set('');

    const step = Math.max(2, Math.ceil(text.length / 120));
    let position = 0;

    this.typingTimer = setInterval(() => {
      position = Math.min(text.length, position + step);
      this.typedText.set(text.slice(0, position));
      this.scrollChat();

      if (position >= text.length) {
        clearInterval(this.typingTimer);
        // keep nodding a moment after the last word
        this.nodTimeout = setTimeout(() => this.typingIndex.set(null), 700);
      }
    }, 22);
  }

  private scrollChat(): void {
    setTimeout(() => {
      const element = this.chatLog?.nativeElement;
      if (element) {
        element.scrollTop = element.scrollHeight;
      }
    });
  }
}
