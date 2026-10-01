import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../auth/auth.service';

@Component({
  selector: 'app-register',
  imports: [FormsModule, RouterLink],
  templateUrl: './register.html',
  styleUrl: './auth-page.scss'
})
export class Register {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

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

  submit(): void {
    this.error.set('');

    if (!this.displayName.trim() || !this.email.trim() || !this.password) {
      this.error.set('Completeaza toate campurile.');
      return;
    }

    if (this.passwordHint()) {
      this.error.set(`Parola nu e suficient de puternica: ${this.passwordHint().toLowerCase()}.`);
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
      next: () => this.router.navigateByUrl('/dashboard'),
      error: (error: HttpErrorResponse) => {
        this.error.set(error.status === 409
          ? 'Exista deja un cont cu acest email.'
          : typeof error.error === 'string' && error.error
            ? error.error
            : 'Contul nu a putut fi creat.');
        this.loading.set(false);
      }
    });
  }
}
