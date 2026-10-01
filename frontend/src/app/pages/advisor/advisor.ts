import { Component, ElementRef, inject, OnInit, signal, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';

import { AdvisorReport, AppLanguage, ChatTurn } from '../../models/insights.model';
import { InsightsApiService } from '../../services/insights-api.service';
import { SettingsStore } from '../../services/settings.store';
import { formatMoney } from '../../shared/format';

/**
 * "Owl": a rule-based financial advisor. Everything is calculated locally from the
 * user's data (no external AI): score with breakdown, verdict, actions and a keyword chat.
 */
@Component({
  selector: 'app-advisor',
  imports: [FormsModule],
  templateUrl: './advisor.html',
  styleUrl: './advisor.scss'
})
export class Advisor implements OnInit {
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
        this.scrollChat();
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

  private scrollChat(): void {
    setTimeout(() => {
      const element = this.chatLog?.nativeElement;
      if (element) {
        element.scrollTop = element.scrollHeight;
      }
    });
  }
}
