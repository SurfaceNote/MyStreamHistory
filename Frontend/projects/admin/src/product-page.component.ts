import { HttpClient } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiResponse } from '../../../src/app/core/api/api-response.model';
import { unwrapData } from '../../../src/app/core/api/api-operators';

export interface ProductWeek { weekStart: string; channelsStreamed: number; engagedChannels: number; }
export interface ActivationCohort { weekStart: string; registered: number; streamedWithin14Days: number; activated: number; }
export interface ProductChannel {
  twitchUserId: number; login: string | null; displayName: string | null; avatarUrl: string | null;
  registeredAt: string | null; streamsCount: number; streamsLast28Days: number; lastStreamStartedAt: string | null;
  isLive: boolean; tracking: 'active' | 'pending' | 'missing' | 'paused';
  lastVisitAt: string | null; lastOverlayAt: string | null; lastPanelAt: string | null;
}
export interface ProductOverview {
  generatedAt: string; activityTrackedSince: string | null;
  weeks: ProductWeek[]; activation: ActivationCohort[]; channels: ProductChannel[];
}

const TRACKING_LABELS: Record<ProductChannel['tracking'], string> = {
  active: 'работает', pending: 'подключается', missing: 'не подключена', paused: 'сбор на паузе',
};

@Component({
  selector: 'admin-product-page',
  styleUrl: './admin-tables.scss',
  template: `<section class="panel-page" [attr.aria-busy]="loading">
    <p class="eyebrow">ПРОДУКТ</p>
    <h1>Продуктовые метрики</h1>
    <p class="muted">Главная метрика — <strong>вовлечённые каналы за неделю</strong>: канал провёл записанный эфир,
      и на той же неделе владелец открывал сайт или использовались оверлей/панель. Недели начинаются в понедельник (UTC).</p>
    @if (data?.activityTrackedSince; as since) {
      <p class="muted">Использование записывается с {{ day(since) }}. До этой даты вовлечённость равна нулю не потому, что каналы не заходили, а потому что данных нет.</p>
    }
    @if (loading) { <p role="status" class="notice">Загружаем метрики…</p> }
    @if (error) { <p role="alert" class="notice error">{{ error }}</p> <button type="button" (click)="load()">Повторить</button> }
    @if (!loading && !error && data) {
      <div class="stat-grid">
        <div class="stat"><span class="muted">Вовлечённые каналы, эта неделя</span><strong>{{ current?.engagedChannels ?? 0 }}</strong><small>из {{ current?.channelsStreamed ?? 0 }} стримивших</small></div>
        <div class="stat"><span class="muted">Прошлая неделя</span><strong>{{ previous?.engagedChannels ?? 0 }}</strong><small>из {{ previous?.channelsStreamed ?? 0 }} стримивших</small></div>
        <div class="stat"><span class="muted">Каналы</span><strong>{{ data.channels.length }}</strong><small>{{ activeLast28Days }} стримили за 28 дней</small></div>
        <div class="stat"><span class="muted">Проблемы с записью</span><strong>{{ problems.length }}</strong><small>подписки Twitch не активны</small></div>
      </div>

      <section class="block" aria-labelledby="weeks-title">
        <h2 id="weeks-title">По неделям</h2>
        <div class="table-scroll"><table>
          <thead><tr><th scope="col">Неделя</th><th scope="col">Стримили</th><th scope="col">Вовлечённые</th><th scope="col">Доля</th></tr></thead>
          <tbody>@for (week of weeksNewestFirst; track week.weekStart) {
            <tr><td>{{ day(week.weekStart) }}</td><td class="numeric">{{ week.channelsStreamed }}</td><td class="numeric">{{ week.engagedChannels }}</td>
              <td class="numeric">{{ share(week.engagedChannels, week.channelsStreamed) }}</td></tr>
          }</tbody>
        </table></div>
      </section>

      <section class="block" aria-labelledby="activation-title">
        <h2 id="activation-title">Активация новых каналов</h2>
        <p class="muted">Активирован — первый эфир записан в течение 14 дней после регистрации, и после его начала владелец заходил на сайт.</p>
        @if (data.activation.length) {
          <div class="table-scroll"><table>
            <thead><tr><th scope="col">Неделя регистрации</th><th scope="col">Зарегистрировались</th><th scope="col">Стримили в первые 14 дней</th><th scope="col">Активированы</th></tr></thead>
            <tbody>@for (cohort of data.activation; track cohort.weekStart) {
              <tr><td>{{ day(cohort.weekStart) }}</td><td class="numeric">{{ cohort.registered }}</td>
                <td class="numeric">{{ cohort.streamedWithin14Days }}</td><td class="numeric">{{ cohort.activated }} ({{ share(cohort.activated, cohort.registered) }})</td></tr>
            }</tbody>
          </table></div>
        } @else { <p class="notice">За выбранный период новых регистраций не было.</p> }
      </section>

      <section class="block" aria-labelledby="channels-title">
        <h2 id="channels-title">Каналы</h2>
        <div class="toolbar">
          <label>Поиск<input type="search" [value]="query" (input)="query = $any($event.target).value" placeholder="Логин или имя"></label>
          <label class="check"><input type="checkbox" [checked]="problemsOnly" (change)="problemsOnly = $any($event.target).checked"> Только с проблемами записи</label>
        </div>
        <div class="table-scroll"><table>
          <caption class="sr-only">Каналы и их использование продукта</caption>
          <thead><tr><th scope="col">Канал</th><th scope="col">Эфиры</th><th scope="col">Последний эфир</th><th scope="col">Запись</th>
            <th scope="col">Владелец заходил</th><th scope="col">Оверлей</th><th scope="col">Панель</th></tr></thead>
          <tbody>@for (channel of visibleChannels; track channel.twitchUserId) {
            <tr>
              <td><div class="channel">
                @if (channel.avatarUrl) { <img [src]="channel.avatarUrl" alt="" width="32" height="32" loading="lazy" referrerpolicy="no-referrer" (error)="channel.avatarUrl = null"> }
                @else { <span class="avatar-placeholder" aria-hidden="true">{{ name(channel).slice(0, 1) }}</span> }
                <div><a [href]="profileUrl(channel)" target="_blank" rel="noopener">{{ name(channel) }}</a>
                  <small>{{ channel.registeredAt ? 'с ' + day(channel.registeredAt) : 'ID ' + channel.twitchUserId }}</small></div>
              </div></td>
              <td class="numeric">{{ channel.streamsCount }}<small>{{ channel.streamsLast28Days }} за 28 дн.</small></td>
              <td>{{ when(channel.lastStreamStartedAt) }} @if (channel.isLive) { <small class="live">В эфире</small> }</td>
              <td><span class="state" [attr.data-state]="channel.tracking">{{ trackingLabel(channel) }}</span></td>
              <td>{{ when(channel.lastVisitAt) }}</td>
              <td>{{ when(channel.lastOverlayAt) }}</td>
              <td>{{ when(channel.lastPanelAt) }}</td>
            </tr>
          } @empty { <tr><td colspan="7" class="muted">Нет каналов по этому фильтру.</td></tr> }</tbody>
        </table></div>
        <p class="muted"><small>Обновлено {{ when(data.generatedAt) }}. Даты показаны в часовом поясе браузера.</small></p>
      </section>
    }
  </section>`
})
export class ProductPageComponent {
  private readonly http = inject(HttpClient);
  private readonly dayFormat = new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' });
  private readonly dateTime = new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
  data: ProductOverview | null = null;
  loading = true;
  error = '';
  query = '';
  problemsOnly = false;

  constructor() { void this.load(); }

  async load(): Promise<void> {
    this.loading = true; this.error = '';
    try {
      this.data = await firstValueFrom(this.http.get<ApiResponse<ProductOverview>>('/api/admin/product/overview', { params: { weeks: 12 } })
        .pipe(unwrapData<ProductOverview>()));
    } catch {
      this.error = 'Не удалось загрузить метрики. Попробуйте ещё раз.';
    } finally { this.loading = false; }
  }

  get current(): ProductWeek | undefined { return this.data?.weeks.at(-1); }
  get previous(): ProductWeek | undefined { return this.data?.weeks.at(-2); }
  get weeksNewestFirst(): ProductWeek[] { return [...(this.data?.weeks ?? [])].reverse(); }
  get problems(): ProductChannel[] { return (this.data?.channels ?? []).filter(c => c.tracking !== 'active'); }
  get activeLast28Days(): number { return (this.data?.channels ?? []).filter(c => c.streamsLast28Days > 0).length; }

  get visibleChannels(): ProductChannel[] {
    const query = this.query.trim().toLowerCase();
    return (this.problemsOnly ? this.problems : this.data?.channels ?? [])
      .filter(c => !query || [c.login, c.displayName].some(value => value?.toLowerCase().includes(query)));
  }

  name(channel: ProductChannel): string { return channel.displayName || channel.login || String(channel.twitchUserId); }
  trackingLabel(channel: ProductChannel): string { return TRACKING_LABELS[channel.tracking] ?? channel.tracking; }
  profileUrl(channel: ProductChannel): string { return `https://mystreamhistory.com/ru/profile/${channel.twitchUserId}`; }
  day(value: string): string { return this.dayFormat.format(new Date(value.length === 10 ? value + 'T00:00:00Z' : value)); }
  when(value: string | null): string { return value ? this.dateTime.format(new Date(value)) : 'нет'; }
  share(part: number, total: number): string { return total ? `${Math.round((part / total) * 100)}%` : '—'; }
}
