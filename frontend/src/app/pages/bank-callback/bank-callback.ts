import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AxMascot } from '../../components/ax-mascot/ax-mascot';
import { TranslatePipe } from '../../i18n/translate.pipe';
import { BankApiService } from '../../services/bank-api.service';

/**
 * The bank sends the user back here (?code=...&state=...). We hand the code to the API,
 * which opens the session and saves the accounts, then go to the bank page with a preview.
 */
@Component({
  selector: 'app-bank-callback',
  imports: [AxMascot, RouterLink, TranslatePipe],
  template: `
    <section class="ui-card callback">
      <app-ax [mood]="error() ? 'worried' : 'thinking'" [size]="110" />
      @if (error()) {
        <h3>{{ 'Conectarea nu a reusit' | t }}</h3>
        <p class="ui-muted">{{ error() | t }}</p>
        <a class="ui-btn" routerLink="/bank">{{ 'Inapoi la contul bancar' | t }}</a>
      } @else {
        <h3>{{ 'Finalizez conectarea cu banca...' | t }}</h3>
        <p class="ui-muted">{{ 'Salvez conturile si citesc tranzactiile. Dureaza cateva secunde.' | t }}</p>
      }
    </section>
  `,
  styles: `
    .callback {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 10px;
      max-width: 560px;
      margin: 60px auto;
      text-align: center;
    }
    h3 { margin: 6px 0 0; }
    a { text-decoration: none; margin-top: 8px; }
  `
})
export class BankCallback implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(BankApiService);

  readonly error = signal('');

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    const code = params.get('code');
    const state = params.get('state');

    if (params.get('error') || !code || !state) {
      this.error.set(params.get('error') === 'access_denied'
        ? 'Ai refuzat accesul in aplicatia bancii, asa ca nu am conectat nimic.'
        : 'Banca nu a trimis inapoi autorizarea. Incearca din nou.');
      return;
    }

    this.api.completeCallback(code, state).subscribe({
      next: connection => this.router.navigate(['/bank'], { queryParams: { connected: connection.id }, replaceUrl: true }),
      error: error => this.error.set(error.status === 0
        ? 'API-ul nu raspunde. Porneste backend-ul (dotnet run --launch-profile https).'
        : typeof error.error === 'string' && /authorization/i.test(error.error)
          ? 'Autorizarea la banca a expirat sau a fost deja folosita. Incearca din nou.'
          : 'Banca nu a putut fi conectata. Incearca din nou.')
    });
  }
}
