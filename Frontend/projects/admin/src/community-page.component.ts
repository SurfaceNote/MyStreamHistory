import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, map } from 'rxjs';
import { CommunityComponent, SEGMENTS } from '../../../src/app/features/community/community.component';
import { CommunitySegment } from '../../../src/app/features/community/community.models';
import { StreamerShortDTO } from '../../../src/app/models/streamer-short.dto';
import { ApiResponse } from '../../../src/app/core/api/api-response.model';
import { unwrapData } from '../../../src/app/core/api/api-operators';

export interface CommunityChannelSummary {
  twitchUserId: string; quality: 'insufficient' | 'provisional' | 'verified'; recalculating: boolean; totalBroadcasts: number;
  segments: Partial<Record<CommunitySegment, number>>; asOf: string | null; calculatedAt: string | null; lastActivityAt: string | null;
}
export interface CommunityChannelRow { twitchId: string; name: string; login: string | null; avatar: string | null; summary: CommunityChannelSummary | null; }

const COLUMNS: CommunitySegment[] = ['core', 'regular', 'occasional', 'new', 'returned', 'fading', 'lost'];
const QUALITY: Record<CommunityChannelSummary['quality'], string> = { verified: 'Готов', provisional: 'Предварительно', insufficient: 'Мало стримов' };

@Component({
  selector: 'admin-community-page', imports: [CommunityComponent],
  styleUrls: ['./admin-tables.scss', './community-page.component.scss'],
  template: `<section class="panel-page" [class.channel-view]="channelId">
    @if (!channelId) {
      <p class="eyebrow">АУДИТОРИЯ</p><h1>Community</h1>
      <p class="muted">Сегменты аудитории по каналам: последние 20 стримов каждого канала, только чтение. Настройки и исключения меняет владелец канала на основном сайте.</p>
      @if (error) { <p role="alert" class="notice error">{{ error }}</p><button type="button" (click)="load()">Повторить</button> }
      @if (loading) { <p role="status" class="notice">Загружаем каналы…</p> }
      @if (!loading && !error) {
        <div class="toolbar">
          <label>Поиск канала<input type="search" [value]="query" (input)="query = $any($event.target).value" placeholder="Название или логин"></label>
          <label class="check"><input type="checkbox" [checked]="withDataOnly" (change)="withDataOnly = !withDataOnly"> Только с данными</label>
        </div>
        <div class="table-scroll">
          <table>
            <caption class="sr-only">Community по каналам</caption>
            <thead><tr>
              <th scope="col">Канал</th><th scope="col" class="numeric">Стримов</th>
              @for (column of columns; track column.id) { <th scope="col" class="numeric"><span class="dot" [style.background]="column.color"></span>{{ column.label }}</th> }
              <th scope="col">Последний стрим</th><th scope="col">Статус</th>
            </tr></thead>
            <tbody>
              @for (row of visibleRows; track row.twitchId) {
                <tr [class.empty-row]="!row.summary" (click)="open(row.twitchId)" tabindex="0" (keydown.enter)="open(row.twitchId)" [attr.aria-label]="'Открыть Community канала ' + row.name">
                  <td><div class="channel">
                    @if (row.avatar) { <img [src]="row.avatar" alt="" width="32" height="32" loading="lazy" referrerpolicy="no-referrer"> }
                    @else { <span class="avatar-placeholder" aria-hidden="true">{{ row.name.slice(0, 1) }}</span> }
                    <div><strong>{{ row.name }}</strong>@if (row.login) { <small>{{ row.login }}</small> }</div>
                  </div></td>
                  <td class="numeric">{{ row.summary?.totalBroadcasts ?? '—' }}</td>
                  @for (column of columns; track column.id) {
                    <td class="numeric">{{ cell(row, column.id) }}</td>
                  }
                  <td class="numeric">{{ date(row.summary?.asOf) }}</td>
                  <td><span class="state" [attr.data-state]="state(row)">{{ status(row) }}</span>
                    @if (row.summary?.calculatedAt) { <small>обновлён {{ dateTime(row.summary!.calculatedAt) }}</small> }</td>
                </tr>
              } @empty { <tr><td [attr.colspan]="columns.length + 4" class="muted">Каналы не найдены.</td></tr> }
            </tbody>
          </table>
        </div>
        <p class="muted hint">«Ядро» и «Постоянные» появляются после 5 стримов. Пока окно из 20 стримов не набрано, отчёт помечен как предварительный.</p>
      }
    } @else {
      <div class="channel-head">
        <button type="button" class="secondary back" (click)="open(null)">← Все каналы</button>
        <div class="title">
          <p class="eyebrow">COMMUNITY · ТОЛЬКО ЧТЕНИЕ</p>
          <h1>{{ channelName }}</h1>
        </div>
        @if (rows.length) {
          <label class="switch">Канал<select [value]="channelId" (change)="open($any($event.target).value)">
            @for (row of switchRows; track row.twitchId) { <option [value]="row.twitchId" [selected]="row.twitchId === channelId">{{ row.name }}{{ row.summary ? '' : ' — нет данных' }}</option> }
          </select></label>
        }
      </div>
    }
  </section>
  @if (channelId) { <div class="embedded-report"><app-community [embedded]="true" /></div> }`
})
export class CommunityPageComponent {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  readonly columns = SEGMENTS.filter(s => COLUMNS.includes(s.id)).map(s => ({ id: s.id, color: s.color, label: SEGMENT_LABELS[s.id] }));
  rows: CommunityChannelRow[] = [];
  error = ''; loading = true; query = ''; withDataOnly = false;
  channelId: string | null = null;

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => this.channelId = params.get('channelId'));
    this.load();
  }

  get visibleRows(): CommunityChannelRow[] {
    const query = this.query.trim().toLowerCase();
    return this.rows.filter(r => (!this.withDataOnly || r.summary) && (!query || r.name.toLowerCase().includes(query) || r.login?.toLowerCase().includes(query)));
  }
  get switchRows(): CommunityChannelRow[] {
    return this.rows.some(r => r.twitchId === this.channelId) ? this.rows
      : [{ twitchId: this.channelId!, name: 'Канал ' + this.channelId, login: null, avatar: null, summary: null }, ...this.rows];
  }
  get channelName(): string { return this.rows.find(r => r.twitchId === this.channelId)?.name ?? 'Канал ' + this.channelId; }

  open(id: string | null): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: { channelId: id || null } });
  }

  load(): void {
    this.error = ''; this.loading = true;
    forkJoin({
      channels: this.http.get<ApiResponse<StreamerShortDTO[]>>('/api/user/all').pipe(unwrapData<StreamerShortDTO[]>()),
      overview: this.http.get<ApiResponse<{ channels: CommunityChannelSummary[] }>>('/api/analytics/community/overview')
        .pipe(unwrapData<{ channels: CommunityChannelSummary[] }>(), map(r => r.channels ?? []))
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: ({ channels, overview }) => { this.rows = buildRows(channels, overview); this.loading = false; },
      error: () => { this.error = 'Не удалось загрузить каналы и сводку Community.'; this.loading = false; }
    });
  }

  cell(row: CommunityChannelRow, segment: CommunitySegment): string {
    const s = row.summary;
    if (!s || s.recalculating && !s.totalBroadcasts) return '—';
    if (s.quality === 'insufficient' && (segment === 'core' || segment === 'regular' || segment === 'fading')) return '—';
    return String(s.segments[segment] ?? 0);
  }
  state(row: CommunityChannelRow): string {
    const s = row.summary;
    if (!s) return 'missing';
    if (s.recalculating) return 'pending';
    return s.quality === 'verified' ? 'active' : 'pending';
  }
  status(row: CommunityChannelRow): string {
    const s = row.summary;
    if (!s) return 'Нет данных';
    if (s.recalculating) return 'Пересчёт';
    return QUALITY[s.quality];
  }
  date(value: string | null | undefined): string {
    return value ? new Date(value + 'T00:00:00Z').toLocaleDateString('ru-RU', { day: 'numeric', month: 'short', timeZone: 'UTC' }) : '—';
  }
  dateTime(value: string | null): string {
    return !value ? '—' : new Date(value).toLocaleString('ru-RU', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
  }
}

const SEGMENT_LABELS: Record<CommunitySegment, string> = {
  core: 'Ядро', regular: 'Постоянные', occasional: 'Иногда', new: 'Новички', returned: 'Вернулись', fading: 'Пропадают', lost: 'Ушли', oneTime: 'Разовые'
};

/** Every known channel, with the community summary where one exists; channels with more data first. */
export function buildRows(channels: StreamerShortDTO[], overview: CommunityChannelSummary[]): CommunityChannelRow[] {
  const summaries = new Map(overview.map(s => [s.twitchUserId, s]));
  const rows: CommunityChannelRow[] = channels.map(c => ({
    twitchId: String(c.twitchId), name: c.displayName || c.login || String(c.twitchId), login: c.login ?? null,
    avatar: c.avatar || null, summary: summaries.get(String(c.twitchId)) ?? null
  }));
  const known = new Set(rows.map(r => r.twitchId));
  for (const s of overview) if (!known.has(s.twitchUserId)) rows.push({ twitchId: s.twitchUserId, name: 'Канал ' + s.twitchUserId, login: null, avatar: null, summary: s });
  return rows.sort((a, b) => (b.summary?.totalBroadcasts ?? -1) - (a.summary?.totalBroadcasts ?? -1) || a.name.localeCompare(b.name, 'ru'));
}
