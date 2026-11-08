import { HttpClient } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { Observable, firstValueFrom } from 'rxjs';
import { ApiResponse } from '../../../src/app/core/api/api-response.model';
import { unwrapData } from '../../../src/app/core/api/api-operators';

export interface ExcludedAccount { login: string; displayName: string | null; addedBy: string; addedAt: string; }
export interface CandidateChannel { streamerTwitchUserId: string; streamerDisplayName: string | null; presenceShare: number; minutes: number; }
export interface BotCandidate {
  twitchUserId: string; login: string; displayName: string; channelsCount: number; highPresenceChannels: number;
  maxPresenceShare: number; totalMinutes: number; messagePoints: number; channels: CandidateChannel[];
}
export interface BotList { accounts: ExcludedAccount[]; candidates: BotCandidate[]; }

interface ChangeResult { error: string | null; }

const LOGIN = /^@?[a-zA-Z0-9_]{1,25}$/;

@Component({
  selector: 'admin-bots-page',
  styleUrl: './admin-tables.scss',
  template: `<section class="panel-page" [attr.aria-busy]="loading">
    <p class="eyebrow">ЧАТ</p>
    <h1>Боты</h1>
    <p class="muted">Аккаунты из этого списка не попадают в топ зрителей, OBS-оверлей и места в рейтинге на всех каналах.
      В списке чата стрима они видны с меткой «бот». Собранные данные не удаляются: если убрать аккаунт из списка, он вернётся в рейтинги.</p>

    <form class="toolbar" (submit)="$event.preventDefault(); add(login)">
      <label>Логин Twitch<input name="login" [value]="login" (input)="login = $any($event.target).value" placeholder="например, kination_bot" autocomplete="off"></label>
      <button type="submit" [disabled]="busy">Добавить в список</button>
    </form>
    @if (message) { <p role="status" class="notice">{{ message }}</p> }
    @if (error) { <p role="alert" class="notice error">{{ error }}</p> }
    @if (loading) { <p role="status" class="notice">Загружаем список…</p> }

    @if (data) {
      <section class="block" aria-labelledby="candidates-title">
        <h2 id="candidates-title">Возможные боты</h2>
        <p class="muted">Подсказка, а не решение. Аккаунт попадает сюда, если сидел в чате не меньше 90% наблюдаемого времени канала
          или не меньше 60% на трёх и более каналах. Время канала оценивается по самому долгому присутствию в его чате;
          каналы короче 5 часов наблюдения не учитываются. Постоянные зрители тоже могут сюда попасть — проверьте перед добавлением.</p>
        @if (data.candidates.length) {
          <div class="table-scroll"><table>
            <caption class="sr-only">Аккаунты с почти постоянным присутствием в чате</caption>
            <thead><tr><th scope="col">Аккаунт</th><th scope="col">Каналы с высоким присутствием</th><th scope="col">Время в чате</th>
              <th scope="col">Баллы чата</th><th scope="col"><span class="sr-only">Действие</span></th></tr></thead>
            <tbody>@for (candidate of data.candidates; track candidate.twitchUserId) {
              <tr>
                <td><strong>{{ candidate.displayName || candidate.login }}</strong><small>{{ candidate.login }}</small></td>
                <td><div class="tags">@for (channel of candidate.channels; track channel.streamerTwitchUserId) {
                  <span>{{ channel.streamerDisplayName || channel.streamerTwitchUserId }} · {{ percent(channel.presenceShare) }}</span>
                }</div><small>{{ candidate.highPresenceChannels }} из {{ candidate.channelsCount }} каналов, где был в чате</small></td>
                <td class="numeric">{{ hours(candidate.totalMinutes) }}</td>
                <td class="numeric">{{ number(candidate.messagePoints) }}</td>
                <td><button type="button" (click)="add(candidate.login)" [disabled]="busy" [attr.aria-label]="'Добавить ' + candidate.login + ' в список ботов'">Это бот</button></td>
              </tr>
            }</tbody>
          </table></div>
        } @else { <p class="notice">Подозрительных аккаунтов не найдено.</p> }
      </section>

      <section class="block" aria-labelledby="list-title">
        <h2 id="list-title">Список ботов ({{ data.accounts.length }})</h2>
        <div class="table-scroll"><table>
          <thead><tr><th scope="col">Логин</th><th scope="col">Имя в чате</th><th scope="col">Добавил</th><th scope="col">Когда</th>
            <th scope="col"><span class="sr-only">Действие</span></th></tr></thead>
          <tbody>@for (account of data.accounts; track account.login) {
            <tr>
              <td><strong>{{ account.login }}</strong></td>
              <td>{{ account.displayName || 'не встречался в чатах' }}</td>
              <td>{{ account.addedBy === 'default' ? 'список по умолчанию' : account.addedBy }}</td>
              <td>{{ date(account.addedAt) }}</td>
              <td><button type="button" class="secondary" (click)="remove(account.login)" [disabled]="busy" [attr.aria-label]="'Убрать ' + account.login + ' из списка ботов'">Убрать</button></td>
            </tr>
          } @empty { <tr><td colspan="5" class="muted">Список пуст: все аккаунты участвуют в рейтингах.</td></tr> }</tbody>
        </table></div>
      </section>
    }
  </section>`
})
export class BotsPageComponent {
  private readonly http = inject(HttpClient);
  private readonly dateFormat = new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'short', year: 'numeric' });
  private readonly numberFormat = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 1 });
  data: BotList | null = null;
  loading = true;
  busy = false;
  login = '';
  message = '';
  error = '';

  constructor() { void this.load(); }

  async load(): Promise<void> {
    this.loading = true;
    try {
      this.data = await firstValueFrom(this.http.get<ApiResponse<BotList>>('/api/admin/bots').pipe(unwrapData<BotList>()));
    } catch {
      this.error = 'Не удалось загрузить список ботов. Обновите страницу.';
    } finally { this.loading = false; }
  }

  async add(value: string): Promise<void> {
    const login = value.trim().replace(/^@/, '').toLowerCase();
    this.message = ''; this.error = '';
    if (!LOGIN.test(value.trim())) { this.error = 'Логин Twitch — от 1 до 25 латинских букв, цифр или подчёркиваний.'; return; }
    await this.change(() => this.http.post<ApiResponse<ChangeResult>>('/api/admin/bots', { login }),
      result => result.error === 'already_excluded' ? `${login} уже в списке.` : `${login} добавлен в список ботов.`);
    if (!this.error) this.login = '';
  }

  async remove(login: string): Promise<void> {
    this.message = ''; this.error = '';
    await this.change(() => this.http.delete<ApiResponse<ChangeResult>>(`/api/admin/bots/${encodeURIComponent(login)}`),
      () => `${login} убран из списка и снова участвует в рейтингах.`);
  }

  private async change(request: () => Observable<ApiResponse<ChangeResult>>, success: (result: ChangeResult) => string): Promise<void> {
    this.busy = true;
    try {
      const result = await firstValueFrom(request().pipe(unwrapData<ChangeResult>()));
      this.message = success(result);
      await this.load();
    } catch {
      this.error = 'Не удалось сохранить изменение. Попробуйте ещё раз.';
    } finally { this.busy = false; }
  }

  percent(share: number): string { return `${Math.round(share * 100)}%`; }
  hours(minutes: number): string { return `${this.numberFormat.format(minutes / 60)} ч`; }
  number(value: number): string { return this.numberFormat.format(value); }
  date(value: string): string { return this.dateFormat.format(new Date(value)); }
}
