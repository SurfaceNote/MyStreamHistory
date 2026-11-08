import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'admin-overview', imports: [RouterLink],
  template: `<section class="panel-page"><p class="eyebrow">ПРОЕКТ</p><h1>Панель управления</h1>
    <p class="muted">Администрирование MyStreamHistory и наблюдение за работой сервисов.</p>
    <div class="overview-grid">
      <a routerLink="/product" class="overview-card"><span>ПРОДУКТ</span><h2>Метрики</h2><p>Вовлечённые каналы по неделям, активация и состояние записи каждого канала.</p></a>
      <a routerLink="/bots" class="overview-card"><span>ЧАТ</span><h2>Боты</h2><p>Список аккаунтов, исключённых из рейтингов, и подсказки о возможных ботах.</p></a>
      <a routerLink="/streams" class="overview-card"><span>ИСТОРИЯ ЭФИРОВ</span><h2>Стримы</h2><p>Все стримы по дням: категории, длительность и уникальные зрители.</p></a>
      <a routerLink="/admin" class="overview-card"><span>01 / TWITCH</span><h2>Подписки и диагностика</h2><p>EventSub, chat-подписки и операции управления.</p></a>
      <a routerLink="/admin/publications" class="overview-card"><span>02 / КОНТЕНТ</span><h2>Публикации</h2><p>Редактор, переводы, медиа и публикация материалов.</p></a>
      <a routerLink="/community" class="overview-card"><span>03 / АУДИТОРИЯ</span><h2>Community</h2><p>Просмотр отчётов по каналам в режиме чтения.</p></a>
      <a routerLink="/monitoring" class="overview-card"><span>04 / МОНИТОРИНГ</span><h2>Производительность</h2><p>SSR, API, ошибки и ресурсы серверов.</p></a>
    </div></section>`
})
export class OverviewComponent { }
