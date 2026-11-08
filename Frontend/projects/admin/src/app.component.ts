import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AdminSession } from './admin-session';
import { LocaleService } from '../../../src/app/i18n/locale.service';

@Component({
  selector: 'admin-root', imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `<div [class.admin-layout]="!!session.username()">@if (session.username()) {<aside class="sidebar">
      <a routerLink="/overview" class="admin-brand">MyStreamHistory<span>ПАНЕЛЬ УПРАВЛЕНИЯ</span></a>
      <nav aria-label="Разделы панели">
        <a routerLink="/overview" routerLinkActive="selected">Обзор</a>
        <a routerLink="/product" routerLinkActive="selected">Продукт</a>
        <a routerLink="/streams" routerLinkActive="selected">Стримы</a>
        <a routerLink="/bots" routerLinkActive="selected">Боты</a>
        <a routerLink="/admin" routerLinkActive="selected" [routerLinkActiveOptions]="{exact:true}">Twitch / диагностика</a>
        <a routerLink="/admin/publications" routerLinkActive="selected">Публикации</a>
        <a routerLink="/community" routerLinkActive="selected">Community</a>
        <a routerLink="/monitoring" routerLinkActive="selected">Производительность</a>
      </nav>
      <a href="https://mystreamhistory.com" target="_blank" rel="noopener" class="site-link">Открыть сайт ↗</a>
      <div class="session"><span>{{ session.username() }}</span><button type="button" (click)="logout()" [disabled]="leaving">Выйти</button></div>
      @if (error) { <p role="alert" class="notice error">{{ error }}</p> }
    </aside>}<main [class.workspace]="!!session.username()"><router-outlet /></main></div>`
})
export class AdminAppComponent {
  readonly session = inject(AdminSession);
  private readonly router = inject(Router);
  private readonly locale = inject(LocaleService);
  leaving = false; error = '';
  constructor() { this.locale.rememberLanguage('ru'); }
  async logout(): Promise<void> {
    this.leaving = true; this.error = '';
    try {
      // Navigate first so the editor's existing save guard can finish before ending the session.
      if (await this.router.navigateByUrl('/overview')) {
        await this.session.logout(); await this.router.navigateByUrl('/login');
      }
    } catch { this.error = 'Не удалось выйти. Повторите попытку.'; }
    finally { this.leaving = false; }
  }
}
