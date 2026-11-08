import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'admin-overview', imports: [RouterLink],
  template: `<section class="panel-page"><p class="eyebrow">ПРОЕКТ</p><h1>Панель управления</h1>
    <p class="muted">Администрирование MyStreamHistory и наблюдение за работой сервисов.</p>
    <div class="overview-grid">
      <a routerLink="/admin" class="overview-card"><span>01 / TWITCH</span><h2>Подписки и диагностика</h2><p>EventSub, chat-подписки и операции управления.</p></a>
      <a routerLink="/admin/publications" class="overview-card"><span>02 / КОНТЕНТ</span><h2>Публикации</h2><p>Редактор, переводы, медиа и публикация материалов.</p></a>
      <a routerLink="/community" class="overview-card"><span>03 / АУДИТОРИЯ</span><h2>Community</h2><p>Просмотр отчётов по каналам в режиме чтения.</p></a>
      <a routerLink="/monitoring" class="overview-card"><span>04 / МОНИТОРИНГ</span><h2>Производительность</h2><p>SSR, API, ошибки и ресурсы серверов.</p></a>
    </div></section>`
})
export class OverviewComponent { }
