import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../auth/auth.service';
import { AxMascot } from '../../components/ax-mascot/ax-mascot';
import { SplashService } from '../../components/welcome-splash/splash.service';
import { I18n } from '../../i18n/i18n.service';
import { LanguageSwitch } from '../../components/language-switch/language-switch';
import { TranslatePipe } from '../../i18n/translate.pipe';

@Component({
  selector: 'app-register',
  imports: [FormsModule, RouterLink, AxMascot, TranslatePipe, LanguageSwitch],
  templateUrl: './register.html',
  styleUrl: './auth-page.scss'
})
export class Register {
  private readonly i18n = inject(I18n);

  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly splash = inject(SplashService);

  displayName = '';
  email = '';
  password = '';
  confirmPassword = '';

  readonly loading = signal(false);
  readonly error = signal('');

  passwordHint(): string {
    if (!this.password) {
      return '';
    }
    if (this.password.length < 8) {
      return 'Minim 8 caractere';
    }
    if (!/\d/.test(this.password)) {
      return 'Adauga cel putin o cifra';
    }
    if (!/[a-z]/.test(this.password)) {
      return 'Adauga cel putin o litera mica';
    }
    return '';
  }

  private describe(error: HttpErrorResponse): string {
    switch (error.status) {
      case 0:
        return 'API-ul nu raspunde. Porneste backend-ul (dotnet run --launch-profile https).';
      case 404:
        return 'API-ul ruleaza o versiune veche, fara conturi. Opreste-l, ruleaza migratia si porneste-l din nou.';
      case 409:
        return 'Exista deja un cont cu acest email.';
      case 500:
        return 'Eroare pe server. Ai rulat "dotnet ef database update" dupa migratia Accounts?';
    }

    if (typeof error.error === 'string' && error.error) {
      return error.error;
    }

    // ASP.NET validation errors: { errors: { Password: ["..."] } }
    const errors = error.error?.errors as Record<string, string[]> | undefined;
    if (errors) {
      return Object.values(errors).flat().join(' ');
    }

    return this.i18n.t('Contul nu a putut fi creat (eroare {status}).', { status: error.status });
  }

  submit(): void {
    this.error.set('');

    if (!this.displayName.trim() || !this.email.trim() || !this.password) {
      this.error.set('Completeaza toate campurile.');
      return;
    }

    if (this.passwordHint()) {
      this.error.set(this.i18n.t('Parola nu e suficient de puternica: {hint}.', { hint: this.i18n.t(this.passwordHint()).toLowerCase() }));
      return;
    }

    if (this.password !== this.confirmPassword) {
      this.error.set('Parolele nu coincid.');
      return;
    }

    this.loading.set(true);

    this.auth.register({
      displayName: this.displayName.trim(),
      email: this.email.trim(),
      password: this.password
    }).subscribe({
      next: response => {
        this.splash.play(response.user.displayName);
        this.router.navigateByUrl('/dashboard');
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(this.describe(error));
        this.loading.set(false);
      }
    });
  }
}
