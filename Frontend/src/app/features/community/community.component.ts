import { TranslationKey } from '../../i18n/translations';
import { TranslatePipe } from '../../i18n/translate.pipe';
import { LocaleService, LocalizedMessage } from '../../i18n/locale.service';
import { afterNextRender, ChangeDetectorRef, Component, effect, ElementRef, inject, NgZone, OnDestroy, untracked, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Chart, ChartDataset, registerables } from 'chart.js';
import { debounceTime, distinctUntilChanged, map, Subject, Subscription } from 'rxjs';
import { CommunityService } from './community.service';
import {
  CommunityActivity, CommunityMember, CommunityMemberFilter, CommunityReport, CommunityResponse, CommunitySegment, SegmentCounts
} from './community.models';
import { AnalyticsService } from '../../service/analytics.service';
import { ADMIN_IDENTITY } from '../../auth/admin-identity';
import { StreamerService } from '../../service/streamer.service';
import { StreamerShortDTO } from '../../models/streamer-short.dto';

Chart.register(...registerables);

export interface SegmentInfo { id: CommunitySegment; label: TranslationKey; rule: TranslationKey; color: string; }

/** Display order: the steady audience first, then movement in and out. */
export const SEGMENTS: SegmentInfo[] = [
  { id: 'core', label: 'community.segment.core', rule: 'community.rule.core', color: '#9b87f5' },
  { id: 'regular', label: 'community.segment.regular', rule: 'community.rule.regular', color: '#5b9cf5' },
  { id: 'occasional', label: 'community.segment.occasional', rule: 'community.rule.occasional', color: '#4fbfa8' },
  { id: 'new', label: 'community.segment.new', rule: 'community.rule.new', color: '#f2c14e' },
  { id: 'returned', label: 'community.segment.returned', rule: 'community.rule.returned', color: '#7bd88f' },
  { id: 'fading', label: 'community.segment.fading', rule: 'community.rule.fading', color: '#f39a55' },
  { id: 'lost', label: 'community.segment.lost', rule: 'community.rule.lost', color: '#ec6a6a' },
  { id: 'oneTime', label: 'community.segment.oneTime', rule: 'community.rule.oneTime', color: '#8c96a8' }
];
const DEFAULT_SERIES: CommunitySegment[] = ['core', 'regular', 'occasional', 'fading'];
type ChartMode = 'segments' | 'flow' | 'visitors';

@Component({
  selector: 'app-community', imports: [TranslatePipe, CommonModule, FormsModule],
  templateUrl: './community.component.html', styleUrl: './community.component.scss'
})
export class CommunityComponent implements OnDestroy {
  readonly locale = inject(LocaleService);
  @ViewChild('trend') trend?: ElementRef<HTMLCanvasElement>;
  @ViewChild('membersPanel') membersPanel?: ElementRef<HTMLElement>;
  private api = inject(CommunityService);
  private analytics = inject(AnalyticsService);
  private auth = inject(ADMIN_IDENTITY);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private streamers = inject(StreamerService);
  private cdr = inject(ChangeDetectorRef);
  private zone = inject(NgZone);
  private subscriptions = new Subscription();
  private channelRequests = new Subscription();
  private searchInput = new Subject<string>();
  private chart?: Chart;
  private poll?: ReturnType<typeof setTimeout>;
  private destroyed = false;
  private reported = false;
  private memberRequest?: Subscription;
  private reportRequest?: Subscription;
  private exclusionsRequest?: Subscription;
  private timeZoneSynced = false;
  private channelsRequested = false;
  private responseReadOnly = false;
  readonly segments = SEGMENTS;
  readonly segmentInfo = Object.fromEntries(SEGMENTS.map(s => [s.id, s])) as Record<CommunitySegment, SegmentInfo>;
  channelId: string | null = null;
  channels: StreamerShortDTO[] = [];
  canViewOtherChannels = this.auth.isAdmin();
  get ownerId(): string { return this.auth.getTwitchIdFromToken() || ''; }
  get selectedChannelId(): string { return this.channelId || this.ownerId; }
  get readOnly(): boolean { return this.auth.isAdmin() || this.responseReadOnly || !!this.channelId; }
  get otherChannels() { return this.channels.filter(c => String(c.twitchId) !== this.ownerId); }
  get missingChannel(): boolean { return !!this.channelId && !this.channels.some(c => String(c.twitchId) === this.channelId); }
  report: CommunityReport | null = null;
  members: CommunityMember[] = [];
  membersTotal: number | null = null;
  exclusions: CommunityResponse['exclusions'] = [];
  nextCursor: string | null = null;
  filter: CommunityMemberFilter = { segment: '', activity: '', search: '' };
  chartMode: ChartMode = 'segments';
  chartRange = 60;
  stacked = false;
  visibleSeries = new Set<CommunitySegment>(DEFAULT_SERIES);
  loading = true;
  membersLoading = false;
  saving = false;
  private _error: LocalizedMessage = '';
  get error(): string { return this.locale.message(this._error); }
  set error(value: LocalizedMessage) { this._error = value; }
  private _settingsError: LocalizedMessage = '';
  get settingsError(): string { return this.locale.message(this._settingsError); }
  set settingsError(value: LocalizedMessage) { this._settingsError = value; }

  constructor() {
    effect(() => { this.locale.language(); untracked(() => this.drawChart()); });
    this.subscriptions.add(this.searchInput.pipe(debounceTime(300), distinctUntilChanged()).subscribe(search => {
      this.filter = { ...this.filter, search };
      this.loadMembers();
    }));
    // Browser only (the report is private), but afterNextRender runs outside NgZone: without zone.run every
    // response, and every view created from it, would live outside the zone and clicks would not re-render.
    afterNextRender(() => this.zone.run(() => this.subscriptions.add(this.route.queryParamMap.pipe(
      map(params => params.get('channelId')), distinctUntilChanged()
    ).subscribe(id => this.openChannel(id)))));
  }

  private openChannel(id: string | null): void {
    clearTimeout(this.poll);
    this.reportRequest?.unsubscribe(); this.memberRequest?.unsubscribe(); this.exclusionsRequest?.unsubscribe();
    this.channelRequests.unsubscribe(); this.channelRequests = new Subscription();
    this.chart?.destroy(); this.chart = undefined;
    this.channelId = id && id !== this.ownerId ? id : null;
    this.responseReadOnly = false;
    this.report = null; this.members = []; this.membersTotal = null; this.exclusions = []; this.nextCursor = null;
    this.filter = { segment: '', activity: '', search: '' };
    this.timeZoneSynced = false; this.reported = false;
    this.saving = false; this.membersLoading = false; this.settingsError = '';
    this.load(); this.loadExclusions();
  }

  viewChannel(id: string): void {
    if (id === this.selectedChannelId) return;
    void this.router.navigate([], { relativeTo: this.route, queryParams: { channelId: id === this.ownerId ? null : id }, queryParamsHandling: 'merge' });
  }

  private applyResponse(response: CommunityResponse): void {
    this.canViewOtherChannels = response.canViewOtherChannels ?? false;
    this.responseReadOnly = response.readOnly ?? false;
    if (this.canViewOtherChannels && !this.channelsRequested) {
      this.channelsRequested = true;
      this.subscriptions.add(this.streamers.getAllStreamers().subscribe({
        next: channels => { this.channels = [...channels].sort((a, b) => a.displayName.localeCompare(b.displayName)); this.cdr.markForCheck(); },
        error: () => { this.channelsRequested = false; this.settingsError = 'community.loadChannelsFailed'; this.cdr.markForCheck(); }
      }));
    }
    this.apply(response.report);
  }

  load(silent = false): void {
    clearTimeout(this.poll);
    this.reportRequest?.unsubscribe();
    this.loading = !silent || !this.report;
    this.error = '';
    this.reportRequest = this.api.report(this.channelId).subscribe({
      next: response => this.applyResponse(response),
      error: () => { this.loading = false; this.error = 'community.loadFailed'; this.poll = setTimeout(() => this.load(true), 60000); this.cdr.markForCheck(); }
    });
  }

  private apply(report: CommunityReport): void {
    if (this.destroyed) return;
    if (!this.readOnly && !this.timeZoneSynced) {
      this.timeZoneSynced = true;
      const zone = Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
      if (!report.timeZoneConfigured || report.timeZoneId !== zone) {
        this.reportRequest?.unsubscribe();
        this.saving = true;
        this.channelRequests.add(this.api.settings(zone).subscribe({
          next: response => { this.saving = false; this.apply(response.report); },
          error: () => {
            this.saving = false;
            this.settingsError = 'community.timezoneFailed';
            this.apply(report);
          }
        }));
        return;
      }
    }
    const changed = this.report?.calculatedAt !== report.calculatedAt;
    this.report = report;
    this.loading = false;
    if (report.recalculating) {
      this.memberRequest?.unsubscribe();
      this.membersLoading = false;
      this.members = []; this.membersTotal = null; this.nextCursor = null;
      this.poll = setTimeout(() => this.load(true), 5000);
    } else {
      if (changed || this.members.length === 0) this.loadMembers();
      this.poll = setTimeout(() => this.load(true), 60000);
      if (!this.readOnly && report.calculatedAt && !this.reported) {
        this.reported = true;
        this.analytics.trackEvent('community_report_open', { quality: report.quality, broadcasts: report.totalBroadcasts });
        const key = `community_last_report_${this.auth.getTwitchIdFromToken()}`;
        try {
          const prior = localStorage.getItem(key);
          if (prior && report.asOf && prior !== report.asOf) this.analytics.trackEvent('community_report_return', { quality: report.quality });
          if (report.asOf) localStorage.setItem(key, report.asOf);
        } catch { /* Usage measurement must not block the report. */ }
      }
    }
    this.cdr.detectChanges();
    this.drawChart();
  }

  // ----- Members -----
  selectSegment(segment: CommunitySegment | '', scroll = false): void {
    this.filter = { ...this.filter, segment: this.filter.segment === segment ? '' : segment };
    if (!this.readOnly) this.analytics.trackEvent('community_level_select', { level: this.filter.segment || 'all' });
    this.loadMembers();
    if (scroll) this.membersPanel?.nativeElement.scrollIntoView?.({ behavior: 'smooth', block: 'start' });
  }
  selectActivity(activity: CommunityActivity | '', scroll = false): void {
    this.filter = { ...this.filter, activity: this.filter.activity === activity ? '' : activity };
    if (!this.readOnly) this.analytics.trackEvent('community_activity_select', { activity: this.filter.activity || 'all' });
    this.loadMembers();
    if (scroll) this.membersPanel?.nativeElement.scrollIntoView?.({ behavior: 'smooth', block: 'start' });
  }
  search(value: string): void { this.searchInput.next(value); }
  loadMembers(more = false): void {
    this.memberRequest?.unsubscribe();
    this.membersLoading = true;
    if (!more) { this.members = []; this.nextCursor = null; }
    this.memberRequest = this.api.members(this.filter, more ? this.nextCursor : null, this.channelId).subscribe({
      next: response => {
        this.members = more ? [...this.members, ...response.members] : response.members;
        this.membersTotal = response.membersTotal ?? null;
        this.nextCursor = response.nextCursor;
        this.membersLoading = false;
        this.cdr.markForCheck();
      },
      error: () => { this.membersLoading = false; this.error = 'community.loadMembersFailed'; this.cdr.markForCheck(); }
    });
  }
  loadExclusions(): void {
    this.exclusionsRequest?.unsubscribe();
    this.exclusionsRequest = this.api.exclusions(this.channelId).subscribe({
      next: r => { this.exclusions = r.exclusions; this.cdr.markForCheck(); },
      error: () => { this.settingsError = 'community.loadExclusionsFailed'; this.cdr.markForCheck(); }
    });
  }
  exclusion(id: string, restore: boolean): void {
    if (this.readOnly) return;
    this.reportRequest?.unsubscribe(); clearTimeout(this.poll);
    this.saving = true; this.settingsError = '';
    this.channelRequests.add((restore ? this.api.restore(id) : this.api.exclude(id)).subscribe({
      next: r => { this.saving = false; this.apply(r.report); this.loadExclusions(); },
      error: () => { this.saving = false; this.settingsError = 'community.updateExclusionsFailed'; this.cdr.markForCheck(); }
    }));
  }
  opened(event: Event): void {
    if (!this.readOnly && (event.target as HTMLDetailsElement).open) this.analytics.trackEvent('community_history_open', { level: this.filter.segment || 'all' });
  }

  // ----- Formatting helpers -----
  format(value: number | null | undefined): string { return value == null ? '—' : this.locale.number(value, 0); }
  count(segment: CommunitySegment, source: SegmentCounts | null | undefined = this.report?.segments): number | null {
    return source ? source[segment] ?? 0 : null;
  }
  delta(segment: CommunitySegment): number | null {
    const prior = this.report?.previousSegments;
    return prior ? (this.count(segment) ?? 0) - (prior[segment] ?? 0) : null;
  }
  deltaLabel(value: number): string { return (value > 0 ? '+' : value < 0 ? '−' : '±') + this.locale.number(Math.abs(value), 0); }
  /** Core and regular cannot be determined until the channel has enough broadcasts. */
  pendingLevel(segment: CommunitySegment): boolean { return this.report?.quality === 'insufficient' && (segment === 'core' || segment === 'regular' || segment === 'fading'); }
  broadcastsUntilReady(): number { return Math.max(0, (this.report?.minBroadcasts ?? 0) - (this.report?.totalBroadcasts ?? 0)); }
  date(value: string | null | undefined, format: 'short' | 'long' = 'short'): string {
    if (!value) return this.locale.t('ui.unknown');
    return new Date(value + 'T00:00:00Z').toLocaleDateString(this.locale.locale(), format === 'short'
      ? { month: 'short', day: 'numeric', timeZone: 'UTC' } : { year: 'numeric', month: 'long', day: 'numeric', timeZone: 'UTC' });
  }
  percent(part: number, whole: number): string {
    return whole ? new Intl.NumberFormat(this.locale.locale(), { style: 'percent', maximumFractionDigits: 0 }).format(part / whole) : '—';
  }
  attendance(member: CommunityMember): { date: string; visited: boolean }[] {
    const dates = this.report?.windowDates ?? [];
    const offset = dates.length - member.attendance.length;
    return [...member.attendance].map((c, i) => ({ date: dates[offset + i] ?? '', visited: c === '1' }));
  }
  lastGroups(last: NonNullable<CommunityReport['lastBroadcast']>) {
    const group = (id: string, segment: CommunitySegment, label: TranslationKey, count: number, people: { twitchUserId: string; displayName: string }[], color: string) =>
      ({ id, segment, label, count, people, color });
    return [
      group('new', 'new', 'community.last.new', last.newCount, last.new, this.segmentInfo.new.color),
      group('returned', 'returned', 'community.last.returned', last.returnedCount, last.returned, this.segmentInfo.returned.color),
      group('missing', 'core', 'community.last.missingCore', last.missingCoreCount, last.missingCore, this.segmentInfo.fading.color)
    ];
  }
  funnelSteps(retention: NonNullable<CommunityReport['retention']>): { label: TranslationKey; value: number }[] {
    return [
      { label: 'community.retention.newcomers', value: retention.newcomers },
      { label: 'community.retention.cameBack', value: retention.cameBack },
      { label: 'community.retention.stayed', value: retention.stayed }
    ];
  }
  memberCountLabel(): string {
    const total = this.membersTotal ?? this.members.length;
    return this.locale.t('community.membersFound', { count: this.locale.number(total, 0) });
  }
  calculatedLabel(): string {
    return this.report?.calculatedAt ? new Intl.DateTimeFormat(this.locale.locale(), {
      timeZone: this.report.timeZoneId, dateStyle: 'short', timeStyle: 'short'
    }).format(new Date(this.report.calculatedAt)) : this.locale.t('community.notCalculated');
  }

  // ----- Chart -----
  hasTrend(): boolean { return (this.report?.trend.length ?? 0) > 1; }
  setChartMode(mode: ChartMode): void { this.chartMode = mode; this.drawChart(); }
  setRange(range: number): void { this.chartRange = range; this.drawChart(); }
  toggleStacked(): void { this.stacked = !this.stacked; this.drawChart(); }
  toggleSeries(segment: CommunitySegment): void {
    if (this.visibleSeries.has(segment)) { if (this.visibleSeries.size > 1) this.visibleSeries.delete(segment); }
    else this.visibleSeries.add(segment);
    this.visibleSeries = new Set(this.visibleSeries);
    this.drawChart();
  }
  private drawChart(): void {
    this.chart?.destroy(); this.chart = undefined;
    const report = this.report;
    if (!this.trend || !report || report.recalculating || !this.hasTrend()) return;
    const points = report.trend.slice(-this.chartRange);
    const labels = points.map(p => this.date(p.date));
    const dashed = (index: number) => points[index]?.provisional ? [5, 4] : undefined;
    let datasets: ChartDataset<'line' | 'bar', (number | null)[]>[];
    if (this.chartMode === 'segments') {
      datasets = SEGMENTS.filter(s => this.visibleSeries.has(s.id)).map(s => ({
        type: 'line' as const, label: this.locale.t(s.label), data: points.map(p => p.segments[s.id] ?? null),
        borderColor: s.color, backgroundColor: s.color + (this.stacked ? '55' : '18'), fill: this.stacked ? 'stack' : false,
        tension: .25, pointRadius: points.length > 30 ? 0 : 2, pointHoverRadius: 4, borderWidth: 2,
        segment: { borderDash: ctx => dashed(ctx.p1DataIndex) }
      }));
    } else if (this.chartMode === 'flow') {
      const bar = (label: TranslationKey, color: string, values: number[], negative = false) => ({
        type: 'bar' as const, label: this.locale.t(label), data: values.map(v => negative ? -v : v),
        backgroundColor: color, borderRadius: 3, stack: 'flow', maxBarThickness: 18
      });
      datasets = [
        bar('community.segment.new', this.segmentInfo.new.color, points.map(p => p.newViewers)),
        bar('community.segment.returned', this.segmentInfo.returned.color, points.map(p => p.returned)),
        bar('community.flow.fading', this.segmentInfo.fading.color, points.map(p => p.becameFading), true),
        bar('community.flow.lost', this.segmentInfo.lost.color, points.map(p => p.becameLost), true)
      ];
    } else {
      datasets = [{
        type: 'line', label: this.locale.t('community.chart.visitors'), data: points.map(p => p.visitors),
        borderColor: '#6f9bff', backgroundColor: '#6f9bff22', fill: true, tension: .25, pointRadius: points.length > 30 ? 0 : 2, borderWidth: 2
      }];
    }
    const stackedLines = this.chartMode === 'segments' && this.stacked;
    this.chart = new Chart(this.trend.nativeElement, {
      type: this.chartMode === 'flow' ? 'bar' : 'line',
      data: { labels, datasets: datasets as ChartDataset<'line', (number | null)[]>[] },
      options: {
        responsive: true, maintainAspectRatio: false, animation: false, locale: this.locale.locale(),
        interaction: { mode: 'index', intersect: false },
        plugins: {
          legend: { display: false },
          tooltip: {
            itemSort: (a, b) => Math.abs(Number(b.raw)) - Math.abs(Number(a.raw)),
            callbacks: {
              label: ctx => `${ctx.dataset.label}: ${this.locale.number(Math.abs(Number(ctx.raw ?? 0)), 0)}`,
              footer: items => points[items[0]?.dataIndex]?.provisional ? this.locale.t('community.chart.provisional') : ''
            }
          }
        },
        scales: {
          x: { stacked: this.chartMode === 'flow' || stackedLines, ticks: { color: '#9aa5b8', maxTicksLimit: 8, maxRotation: 0 }, grid: { display: false } },
          y: {
            stacked: this.chartMode === 'flow' || stackedLines, beginAtZero: true,
            ticks: { color: '#9aa5b8', precision: 0, callback: v => this.locale.number(Math.abs(Number(v)), 0) },
            grid: { color: '#ffffff12' }
          }
        }
      }
    });
  }
  ngOnDestroy(): void {
    this.destroyed = true; clearTimeout(this.poll); this.reportRequest?.unsubscribe(); this.memberRequest?.unsubscribe(); this.exclusionsRequest?.unsubscribe(); this.channelRequests.unsubscribe(); this.subscriptions.unsubscribe(); this.chart?.destroy();
  }
}
