import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, ElementRef, inject, OnDestroy, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Chart, registerables } from 'chart.js';
import { Subscription } from 'rxjs';
Chart.register(...registerables);
interface Metric { labels: Record<string, string>; value: number | null; timestamp: number; }
interface Monitoring {
  state: 'ready' | 'unconfigured' | 'unavailable'; sentryUrl?: string | null; updatedAt: string;
  metrics?: Record<string, Metric[]>; series?: { timestamp: number; value: number | null }[];
}
@Component({
  selector: 'admin-monitoring', imports: [FormsModule, DatePipe, DecimalPipe],
  template: `<section class="panel-page"><p class="eyebrow">НАБЛЮДАЕМОСТЬ</p><h1>Производительность</h1>
    <p class="muted">SSR и API: показатели за выбранный период. CPU и память — текущие значения. Время SSR включает ожидание API.</p>
    <div class="monitor-controls"><label>Период<select [(ngModel)]="range" (ngModelChange)="load()"><option value="1h">Час</option><option value="24h">Сутки</option><option value="7d">Неделя</option></select></label>
      <button type="button" (click)="load()" [disabled]="loading">{{ loading ? 'Обновляем…' : 'Обновить' }}</button>
      @if (data?.sentryUrl) { <a [href]="data!.sentryUrl" target="_blank" rel="noopener">Ошибки и browser tracing в Sentry ↗</a> }
    </div>
    @if (data) { <p class="muted">Последнее обновление: {{ data.updatedAt | date:'dd.MM.yyyy HH:mm:ss' }}</p> }
    @if (error) { <p role="alert" class="notice error">{{ error }}</p> }
    @if (data?.state === 'unconfigured') { <p class="notice">Источник метрик ещё не подключён. Настройте Prometheus и сбор показателей по инструкции развёртывания AdminService.</p> }
    @if (data?.state === 'ready') {
      <div class="stat-grid">
        @for (card of cards; track card.key) { <article class="stat"><span>{{ card.label }}</span><strong>{{ format(card.key, card.unit) }}</strong><small>{{ card.hint }}</small></article> }
      </div>
      <article class="stat" style="margin-top:20px"><h2>SSR p95 во времени</h2><p class="muted">Скользящее окно 5 минут. Пропуски означают отсутствие наблюдений.</p><div class="chart-box"><canvas #timeline aria-label="Время SSR p95, миллисекунды" role="img"></canvas></div></article>
      <article class="stat" style="margin-top:20px"><h2>Сбор метрик</h2>
        @for (target of rows('scrapes'); track $index) { <p>{{ target.labels['job'] }} / {{ target.labels['instance'] }}: {{ target.value === 1 ? 'доступен' : 'недоступен' }}</p> }
        @if (!rows('scrapes').length) { <p class="muted">Нет настроенных источников.</p> }
      </article>
      @for (table of tables; track table.key) {
        <article class="stat" style="margin-top:20px;overflow:auto"><h2>{{ table.label }}</h2><table style="width:100%;text-align:left"><thead><tr><th>Сервис / язык</th><th>Маршрут</th><th>p95, мс</th></tr></thead><tbody>
          @for (row of rows(table.key); track $index) { <tr><td>{{ row.labels['service'] || row.labels['language'] }}</td><td><code>{{ row.labels['route'] }}</code></td><td>{{ row.value === null ? 'Нет данных' : ((row.value * 1000) | number:'1.0-0') }}</td></tr> }
        </tbody></table>@if (!rows(table.key).length) { <p class="muted">Нет наблюдений за выбранный период.</p> }</article>
      }
    }
  </section>`
})
export class MonitoringComponent implements OnDestroy {
  private readonly http = inject(HttpClient);
  private request?: Subscription;
  private chart?: Chart;
  private canvas?: HTMLCanvasElement;
  @ViewChild('timeline') set timeline(value: ElementRef<HTMLCanvasElement> | undefined) {
    this.canvas = value?.nativeElement; this.draw();
  }
  range = '1h'; loading = false; error = ''; data: Monitoring | null = null;
  readonly tables = [{ key: 'pageP95', label: 'Публичные страницы' }, { key: 'apiP95', label: 'API по сервисам' }];
  readonly cards = [
    { key: 'ssrRps', label: 'Запросов SSR / с', unit: 'number', hint: 'Средняя частота за период' },
    { key: 'ssrP50', label: 'SSR p50', unit: 'ms', hint: 'Медиана времени подготовки HTML' },
    { key: 'ssrP95', label: 'SSR p95', unit: 'ms', hint: '95% рендеров укладываются в это время' },
    { key: 'ssrErrors', label: 'Ошибки SSR', unit: 'percent', hint: 'Доля ответов 5xx' },
    { key: 'ssrApiPerPage', label: 'API-вызовов на страницу', unit: 'number', hint: 'Среднее число запросов внутри SSR' },
    { key: 'ssrHtmlBytes', label: 'Размер HTML', unit: 'kb', hint: 'Средний размер без сжатия' },
    { key: 'ssrMemoryBytes', label: 'Память SSR', unit: 'mb', hint: 'RSS всех экземпляров' },
    { key: 'ssrCpuCores', label: 'CPU SSR', unit: 'cpu', hint: '100% = одно ядро, окно 5 минут' },
    { key: 'eventLoopDelay', label: 'Задержка event loop', unit: 'ms', hint: 'Наибольший p95 между сборами' }
  ];
  constructor() { this.load(); }
  rows(key: string): Metric[] { return [...(this.data?.metrics?.[key] || [])].sort((a, b) => (b.value ?? -1) - (a.value ?? -1)); }
  format(key: string, unit: string): string {
    const v = this.data?.metrics?.[key]?.[0]?.value;
    if (v == null) return 'Нет данных';
    const factor = unit === 'ms' ? 1000 : unit === 'percent' || unit === 'cpu' ? 100 : unit === 'mb' ? 1 / 1048576 : unit === 'kb' ? 1 / 1024 : 1;
    const suffix = unit === 'ms' ? ' мс' : unit === 'percent' || unit === 'cpu' ? '%' : unit === 'mb' ? ' МиБ' : unit === 'kb' ? ' КиБ' : '';
    return (v * factor).toLocaleString('ru-RU', { maximumFractionDigits: 2 }) + suffix;
  }
  load(): void {
    this.request?.unsubscribe(); this.loading = true; this.error = '';
    this.request = this.http.get<Monitoring>('/api/monitoring', { params: { range: this.range } }).subscribe({
      next: data => { this.data = data; this.loading = false; this.draw(); },
      error: error => { this.loading = false; this.error = error.status === 401 ? 'Сессия завершилась. Войдите в панель заново.' : 'Источник метрик недоступен. Показанные ранее данные могут быть устаревшими.'; }
    });
  }
  private draw(): void {
    this.chart?.destroy(); this.chart = undefined;
    if (!this.canvas || !this.data?.series) return;
    this.chart = new Chart(this.canvas, {
      type: 'line', data: { labels: this.data.series.map(point => new Date(point.timestamp * 1000).toLocaleString('ru-RU')),
        datasets: [{ label: 'SSR p95, мс', data: this.data.series.map(point => point.value === null ? null : point.value * 1000), borderColor: '#bca0ed', pointRadius: 0, borderWidth: 2, spanGaps: false }] },
      options: { responsive: true, maintainAspectRatio: false, animation: false, scales: { y: { beginAtZero: true }, x: { ticks: { maxTicksLimit: 6 } } }, plugins: { legend: { display: false } } }
    });
  }
  ngOnDestroy(): void { this.request?.unsubscribe(); this.chart?.destroy(); }
}
