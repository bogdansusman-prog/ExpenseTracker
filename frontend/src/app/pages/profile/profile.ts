import { Component, computed, inject, OnInit, signal, WritableSignal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';

import { AuthService } from '../../auth/auth.service';
import { AxMascot, AxMood } from '../../components/ax-mascot/ax-mascot';
import { resizeImageToDataUrl } from '../../shared/image-resize';

@Component({
  selector: 'app-profile',
  imports: [FormsModule, AxMascot],
  templateUrl: './profile.html',
  styleUrl: './profile.scss'
})
export class Profile implements OnInit {
  readonly auth = inject(AuthService);

  readonly user = this.auth.user;

  displayName = '';
  currentPassword = '';
  newPassword = '';
  confirmPassword = '';

  readonly savingName = signal(false);
  readonly savingAvatar = signal(false);
  readonly savingPassword = signal(false);
  readonly dragOver = signal(false);

  readonly message = signal('');
  readonly error = signal('');

  readonly initial = computed(() => (this.user()?.displayName || '?').slice(0, 1).toUpperCase());

  readonly memberSince = computed(() => {
    const created = this.user()?.createdAt;
    return created
      ? new Date(created).toLocaleDateString('ro-RO', { day: 'numeric', month: 'long', year: 'numeric' })
      : '';
  });

  readonly axMood = computed<AxMood>(() => {
    if (this.savingAvatar() || this.savingName() || this.savingPassword()) {
      return 'thinking';
    }
    if (this.error()) {
      return 'worried';
    }
    return this.message() ? 'happy' : 'idle';
  });

  ngOnInit(): void {
    this.displayName = this.user()?.displayName ?? '';

    // Make sure we show the latest data (e.g. avatar changed on another device).
    this.auth.refreshMe().subscribe({
      next: user => (this.displayName = user.displayName),
      error: () => undefined
    });
  }

  // ---------- Avatar ----------

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';

    if (file) {
      this.uploadAvatar(file);
    }
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragOver.set(false);

    const file = event.dataTransfer?.files?.[0];
    if (file) {
      this.uploadAvatar(file);
    }
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.dragOver.set(true);
  }

  removeAvatar(): void {
    this.reset();
    this.savingAvatar.set(true);

    this.auth.deleteAvatar().subscribe({
      next: () => this.done(this.savingAvatar, 'Poza de profil a fost stearsa.'),
      error: error => this.fail(this.savingAvatar, error)
    });
  }

  private async uploadAvatar(file: File): Promise<void> {
    this.reset();

    if (file.size > 15 * 1024 * 1024) {
      this.error.set('Imaginea e prea mare (maxim 15 MB).');
      return;
    }

    this.savingAvatar.set(true);

    try {
      const dataUrl = await resizeImageToDataUrl(file, 256);

      this.auth.updateAvatar(dataUrl).subscribe({
        next: () => this.done(this.savingAvatar, 'Poza de profil a fost actualizata.'),
        error: error => this.fail(this.savingAvatar, error)
      });
    } catch {
      this.savingAvatar.set(false);
      this.error.set('Fisierul nu este o imagine valida.');
    }
  }

  // ---------- Name ----------

  saveName(): void {
    this.reset();
    const name = this.displayName.trim();

    if (!name) {
      this.error.set('Numele nu poate fi gol.');
      return;
    }

    this.savingName.set(true);

    this.auth.updateProfile(name).subscribe({
      next: () => this.done(this.savingName, 'Numele a fost schimbat.'),
      error: error => this.fail(this.savingName, error)
    });
  }

  // ---------- Password ----------

  passwordHint(): string {
    if (!this.newPassword) {
      return '';
    }
    if (this.newPassword.length < 8) {
      return 'Minim 8 caractere';
    }
    if (!/\d/.test(this.newPassword)) {
      return 'Adauga cel putin o cifra';
    }
    if (!/[a-z]/.test(this.newPassword)) {
      return 'Adauga cel putin o litera mica';
    }
    return '';
  }

  passwordStrength(): number {
    const password = this.newPassword;
    let score = 0;
    if (password.length >= 8) score++;
    if (password.length >= 12) score++;
    if (/\d/.test(password) && /[a-z]/.test(password)) score++;
    if (/[A-Z]/.test(password)) score++;
    if (/[^A-Za-z0-9]/.test(password)) score++;
    return password ? score : 0;
  }

  changePassword(): void {
    this.reset();

    if (!this.currentPassword || !this.newPassword) {
      this.error.set('Completeaza parola actuala si parola noua.');
      return;
    }
    if (this.passwordHint()) {
      this.error.set(`Parola noua nu e suficient de puternica: ${this.passwordHint().toLowerCase()}.`);
      return;
    }
    if (this.newPassword !== this.confirmPassword) {
      this.error.set('Parolele noi nu coincid.');
      return;
    }

    this.savingPassword.set(true);

    this.auth.changePassword(this.currentPassword, this.newPassword).subscribe({
      next: () => {
        this.currentPassword = '';
        this.newPassword = '';
        this.confirmPassword = '';
        this.done(this.savingPassword, 'Parola a fost schimbata.');
      },
      error: error => this.fail(this.savingPassword, error)
    });
  }

  // ---------- helpers ----------

  private reset(): void {
    this.message.set('');
    this.error.set('');
  }

  private done(flag: WritableSignal<boolean>, text: string): void {
    flag.set(false);
    this.message.set(text);
  }

  private fail(flag: WritableSignal<boolean>, error: HttpErrorResponse): void {
    flag.set(false);

    if (error.status === 0) {
      this.error.set('API-ul nu raspunde.');
    } else if (typeof error.error === 'string' && error.error) {
      this.error.set(error.error === 'The current password is incorrect.' ? 'Parola actuala este gresita.' : error.error);
    } else {
      this.error.set('Ceva n-a mers. Incearca din nou.');
    }
  }
}
