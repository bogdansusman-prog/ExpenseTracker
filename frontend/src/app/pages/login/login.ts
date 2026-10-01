import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthService } from '../../auth/auth.service';
import { AxMascot } from '../../components/ax-mascot/ax-mascot';

@Component({
  selector: 'app-login',
  imports: [FormsModule, RouterLink, AxMascot],
  templateUrl: './login.html',
  styleUrl: '../register/auth-page.scss'
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  email = '';
  password = '';

  readonly loading = signal(false);
  readonly error = signal('');
  readonly expired = this.route.snapshot.queryParamMap.has('expired');

  submit(): void {
    if (!this.email || !this.password) {
      this.error.set('Completeaza emailul si parola.');
      return;
    }

    this.loading.set(true);
    this.error.set('');

    this.auth.login({ email: this.email.trim(), password: this.password }).subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') || '/dashboard';
        this.router.navigateByUrl(returnUrl);
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(error.status === 401
          ? 'Email sau parola gresita.'
          : error.status === 0
            ? 'API-ul nu raspunde. Porneste backend-ul.'
            : 'Autentificarea a esuat. Incearca din nou.');
        this.loading.set(false);
      }
    });
  }
}
