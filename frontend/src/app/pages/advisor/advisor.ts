import { Component, ElementRef, inject, OnInit, signal, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';

import { AdvisorReport, AdvisorStatus, AppLanguage, ChatTurn } from '../../models/insights.model';
import { InsightsApiService } from '../../services/insights-api.service';
import { SettingsStore } from '../../services/settings.store';
import { formatMoney } from '../../shared/format';

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

  readonly status = signal<AdvisorStatus | null>(null);
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
      'Unde pot sa economisesc 300 lei luna asta?',
      'Sunt pe drumul bun fata de luna trecuta?',
      'Ce abonamente ar trebui sa anulez?',
      'Cate ore de munca m-au costat iesirile in oras?'
    ],
    en: [
      'Where can I save 300 lei this month?',
      'Am I doing better than last month?',
      'Which subscriptions should I cancel?',
      'How many work hours did eating out cost me?'
    ]
  };

  ngOnInit(): void {
    this.settings.load();
    this.api.getAdvisorStatus().subscribe({
      next: status => {
        this.status.set(status);
        if (status.configured) {
          this.loadReport(false);
        }
      },
      error: () => this.status.set({ configured: false, model: '' })
    });
  }

  language(): AppLanguage {
    return this.settings.language();
  }

  setLanguage(language: AppLanguage): void {
    if (language === this.language()) {
      return;
    }

    this.settings.save({ hourlyRate: this.settings.hourlyRate(), language }).subscribe(() => this.loadReport(false));
  }

  loadReport(refresh: boolean): void {
    this.loadingReport.set(true);
    this.reportError.set('');

    this.api.getAdvisorReport(this.language(), refresh).subscribe({
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

  verdictLabel(verdict: string): string {
    const labels: Record<string, Record<AppLanguage, string>> = {
      good: { ro: 'Esti pe drumul bun', en: 'You are on track' },
      ok: { ro: 'Merge, dar se poate mai bine', en: 'Okay, but could be better' },
      bad: { ro: 'Atentie, esti pe minus', en: 'Warning: you are off track' }
    };
    return (labels[verdict] ?? labels['ok'])[this.language()];
  }

  private describeError(error: HttpErrorResponse): string {
    if (error.status === 503) {
      return 'Consilierul AI nu este disponibil. Verifica cheia API (Anthropic:ApiKey) si conexiunea la internet.';
    }
    if (error.status === 0) {
      return 'API-ul nu raspunde. Porneste backend-ul (dotnet run).';
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
