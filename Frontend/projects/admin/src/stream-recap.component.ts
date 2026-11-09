import { HttpClient } from '@angular/common/http';
import { Component, Input, OnChanges, OnDestroy, inject } from '@angular/core';
import { Subscription } from 'rxjs';
import { ApiResponse } from '../../../src/app/core/api/api-response.model';
import { unwrapData } from '../../../src/app/core/api/api-operators';
import { RecapViewer, StreamRecap } from '../../../src/app/models/stream-recap.model';

interface PeopleGroup { title: string; hint: string; total: number; viewers: RecapViewer[]; note: (viewer: RecapViewer) => string; }

/** The stream recap the streamer sees on their stream page. Only the streamer and the admin panel can read it. */
@Component({
  selector: 'admin-stream-recap',
  template: `
    @if (loading) { <p role="status" class="notice">Загружаем итоги…</p> }
    @if (error) {
      <p role="alert" class="notice error">{{ error }}</p>
      <button type="button" class="secondary" (click)="load()">Повторить</button>
    }
    @if (recap; as r) {
      <div class="recap">
        <dl class="tiles">
          <div><dt>Длительность</dt><dd>{{ duration(r.durationMinutes) }}</dd><small>{{ durationNote(r) }}</small></div>
          <div><dt>Пик зрителей</dt><dd>{{ r.peakViewers ?? '—' }}</dd><small>{{ r.averageViewers != null ? 'в среднем ' + number(r.averageViewers) : 'нет замеров' }}</small></div>
          <div><dt>В чате</dt><dd>{{ r.viewerDataAvailable ? r.chatAccounts : '—' }}</dd><small>{{ chatNote(r) }}</small></div>
          <div><dt>Впервые в чате</dt><dd>{{ r.viewerDataAvailable && r.previousStreamsCount > 0 ? r.newCount : '—' }}</dd><small>{{ r.viewerDataAvailable ? 'вернулись после перерыва: ' + r.returningCount : 'нет данных о зрителях' }}</small></div>
        </dl>
        <p class="muted basis">
          @if (r.comparisonStreamsCount > 0) { Сравнение с {{ r.comparisonStreamsCount }} {{ plural(r.comparisonStreamsCount, 'предыдущим стримом', 'предыдущими стримами', 'предыдущими стримами') }}. }
          @else { Это первый записанный стрим канала, сравнивать не с чем. }
          @if (r.isLive) { Стрим ещё идёт, данные неполные. }
        </p>
        @if (r.playthroughs.length) {
          <div class="tags">@for (p of r.playthroughs; track p.playthroughId) { <span>{{ p.gameName }} — стрим {{ p.streamNumber }} из {{ p.streamsCount }} в прохождении</span> }</div>
        }
        @if (r.viewerDataAvailable) {
          <div class="groups">
            @for (group of groups(r); track group.title) {
              <section [attr.aria-label]="group.title">
                <h3>{{ group.title }} <span class="muted">{{ group.total }}</span></h3>
                <p class="muted">{{ group.hint }}</p>
                @if (group.viewers.length) {
                  <ul>@for (viewer of group.viewers; track viewer.twitchUserId) {
                    <li><strong>{{ viewer.displayName || viewer.login }}</strong>@if (viewer.displayName && viewer.login !== viewer.displayName.toLowerCase()) { <span class="muted"> {{ viewer.login }}</span> }<small>{{ group.note(viewer) }}</small></li>
                  }</ul>
                  @if (group.total > group.viewers.length) { <p class="muted">и ещё {{ group.total - group.viewers.length }}</p> }
                } @else { <p class="muted">Никого</p> }
              </section>
            }
          </div>
        } @else { <p class="notice">Нет данных о зрителях этого стрима.</p> }
      </div>
    }`,
  styles: [`
    :host { display: block; }
    .recap { display: grid; gap: 14px; }
    .tiles { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 10px; margin: 0; }
    .tiles div { padding: 12px 14px; border: 1px solid #32303d; border-radius: 10px; background: #20202a; }
    dt { color: #aaa7b6; font-size: 11px; }
    dd { margin: 4px 0 0; font-size: 20px; font-weight: 600; font-variant-numeric: tabular-nums; }
    small { display: block; margin-top: 4px; color: #aaa7b6; font-size: 11px; }
    .basis { margin: 0; font-size: 12px; }
    .tags { display: flex; flex-wrap: wrap; gap: 6px; }
    .tags span { padding: 4px 8px; border-radius: 5px; background: #292135; }
    .groups { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px; }
    h3 { font-size: 14px; margin: 0; }
    .groups p { margin: 4px 0 8px; font-size: 12px; }
    ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
    li { overflow-wrap: anywhere; }
    .muted { color: #aaa7b6; }
  `]
})
export class StreamRecapComponent implements OnChanges, OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly numberFormat = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 1 });
  private sub?: Subscription;
  @Input({ required: true }) streamId!: string;
  recap: StreamRecap | null = null;
  loading = false;
  error = '';

  ngOnChanges(): void { this.load(); }
  ngOnDestroy(): void { this.sub?.unsubscribe(); }

  load(): void {
    this.sub?.unsubscribe();
    this.recap = null; this.error = ''; this.loading = true;
    this.sub = this.http.get<ApiResponse<StreamRecap>>(`/api/admin/streams/${encodeURIComponent(this.streamId)}/recap`)
      .pipe(unwrapData<StreamRecap>())
      .subscribe({
        next: recap => { this.recap = recap; this.loading = false; },
        error: () => { this.error = 'Не удалось загрузить итоги стрима.'; this.loading = false; }
      });
  }

  groups(r: StreamRecap): PeopleGroup[] {
    return [
      { title: 'Впервые в чате', hint: 'Не было ни на одном прошлом стриме', total: r.newCount, viewers: r.newViewers, note: v => this.watched(v) },
      { title: 'Вернулись', hint: 'Пропустили 3 стрима подряд или больше', total: r.returningCount, viewers: r.returningViewers,
        note: v => v.missedStreams ? `после ${v.missedStreams} ${this.plural(v.missedStreams, 'пропущенного стрима', 'пропущенных стримов', 'пропущенных стримов')}` : this.watched(v) },
      { title: 'Постоянные', hint: '3 стрима подряд или больше', total: r.streakViewers.length, viewers: r.streakViewers,
        note: v => v.streak ? `${v.streak} ${this.plural(v.streak, 'стрим', 'стрима', 'стримов')} подряд` : this.watched(v) },
      { title: 'Активнее всех в чате', hint: 'По очкам активности в чате', total: r.topChatters.length, viewers: r.topChatters,
        note: v => `${this.number(v.chatPoints)} ${this.plural(Math.round(v.chatPoints), 'очко', 'очка', 'очков')}` }
    ];
  }

  durationNote(r: StreamRecap): string {
    if (r.isLive) return 'стрим ещё идёт';
    if (r.averageDurationMinutes == null) return 'не с чем сравнить';
    const delta = r.durationMinutes - r.averageDurationMinutes;
    if (Math.abs(delta) < Math.max(10, r.averageDurationMinutes * 0.1)) return 'как обычно';
    return `на ${this.duration(Math.abs(delta))} ${delta > 0 ? 'дольше' : 'короче'} обычного`;
  }

  chatNote(r: StreamRecap): string {
    if (!r.viewerDataAvailable) return 'нет данных о зрителях';
    if (r.averageChatAccounts == null) return 'не с чем сравнить';
    const delta = Math.round(r.chatAccounts - r.averageChatAccounts);
    if (delta === 0) return 'как обычно';
    return `на ${Math.abs(delta)} ${delta > 0 ? 'больше' : 'меньше'} обычного`;
  }

  duration(minutes: number): string {
    const total = Math.round(minutes);
    const hours = Math.floor(total / 60);
    return hours > 0 ? `${hours} ч ${total % 60} мин` : `${total} мин`;
  }

  number(value: number): string { return this.numberFormat.format(value); }

  plural(count: number, one: string, few: string, many: string): string {
    const n = Math.abs(count) % 100, last = n % 10;
    if (n > 10 && n < 20) return many;
    if (last === 1) return one;
    return last >= 2 && last <= 4 ? few : many;
  }

  private watched(viewer: RecapViewer): string { return `на стриме ${this.duration(viewer.minutesWatched)}`; }
}
