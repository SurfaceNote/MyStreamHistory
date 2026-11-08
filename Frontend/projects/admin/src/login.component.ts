import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminSession } from './admin-session';

@Component({
  selector: 'admin-login', imports: [FormsModule],
  template: `<main class="login-page"><form (ngSubmit)="login()" class="login-card">
    <p class="eyebrow">MYSTREAMHISTORY / ADMIN</p><h1>Вход в панель</h1>
    <p class="muted">Управление проектом и диагностика</p>
    <label>Логин<input name="username" [(ngModel)]="username" autocomplete="username" required maxlength="100" /></label>
    <label>Пароль<input name="password" type="password" [(ngModel)]="password" autocomplete="current-password" required maxlength="1024" /></label>
    @if (error) { <p role="alert" class="notice error">{{ error }}</p> }
    <button type="submit" [disabled]="busy || !username || !password">{{ busy ? 'Входим…' : 'Войти' }}</button>
  </form></main>`
})
export class LoginComponent {
  private readonly session = inject(AdminSession);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  username = ''; password = ''; error = ''; busy = false;
  async login(): Promise<void> {
    if (this.busy) return;
    this.busy = true; this.error = '';
    try {
      await this.session.login(this.username, this.password);
      const target = this.route.snapshot.queryParamMap.get('returnUrl') || '/overview';
      await this.router.navigateByUrl(/^\/(?:overview|admin(?:\/|$)|community|monitoring)(?:[^\\]*)$/.test(target) ? target : '/overview');
    } catch (error) {
      const status = (error as { status?: number }).status;
      this.error = status === 429 ? 'Слишком много попыток. Повторите через минуту.'
        : status === 401 ? 'Неверный логин или пароль.' : 'Не удалось войти. Проверьте соединение и повторите попытку.';
    } finally { this.busy = false; this.password = ''; }
  }
}
