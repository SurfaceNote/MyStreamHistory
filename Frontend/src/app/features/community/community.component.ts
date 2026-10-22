import { TranslationKey } from '../../i18n/translations';
import { TranslatePipe } from '../../i18n/translate.pipe';
import { LocaleService, LocalizedMessage } from '../../i18n/locale.service';
import { afterNextRender, ChangeDetectorRef, Component, effect, ElementRef, inject, OnDestroy, untracked, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Chart, registerables } from 'chart.js';
import { distinctUntilChanged, map, Subscription } from 'rxjs';
import { CommunityService } from './community.service';
import { CommunityMember, CommunityReport, CommunityResponse } from './community.models';
import { AnalyticsService } from '../../service/analytics.service';
import { AuthService } from '../../auth/auth.service';
import { StreamerService } from '../../service/streamer.service';
import { StreamerShortDTO } from '../../models/streamer-short.dto';

Chart.register(...registerables);

@Component({
  selector: 'app-community', imports: [TranslatePipe, CommonModule, FormsModule],
  templateUrl: './community.component.html', styleUrl: './community.component.scss'
})
export class CommunityComponent implements OnDestroy {
  readonly locale = inject(LocaleService);
  @ViewChild('trend') trend?: ElementRef<HTMLCanvasElement>;
  private api = inject(CommunityService);
  private analytics = inject(AnalyticsService);
  private auth = inject(AuthService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private streamers = inject(StreamerService);
  private cdr = inject(ChangeDetectorRef);
  private subscriptions = new Subscription();
  private channelRequests = new Subscription();
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
  channelId: string | null = null;
  channels: StreamerShortDTO[] = [];
  canViewOtherChannels = false;
  get ownerId(): string { return this.auth.getTwitchIdFromToken() || ''; }
  get selectedChannelId(): string { return this.channelId || this.ownerId; }
  get readOnly(): boolean { return this.responseReadOnly || !!this.channelId; }
  get otherChannels() { return this.channels.filter(c => String(c.twitchId) !== this.ownerId); }
  get missingChannel(): boolean { return !!this.channelId && !this.channels.some(c => String(c.twitchId) === this.channelId); }
  report: CommunityReport | null = null;
  members: CommunityMember[] = [];
  exclusions: CommunityResponse['exclusions'] = [];
  nextCursor: string | null = null;
  selectedLevel = 'core';
  loading = true;
  membersLoading = false;
  saving = false;
  private _error: LocalizedMessage = '';
  get error(): string { return this.locale.message(this._error); }
  set error(value: LocalizedMessage) { this._error = value; }
  private _settingsError: LocalizedMessage = '';
  get settingsError(): string { return this.locale.message(this._settingsError); }
  set settingsError(value: LocalizedMessage) { this._settingsError = value; }
  readonly levels = [
    { id: 'core', label: 'community.core', rule: 'community.coreRule' },
    { id: 'regular', label: 'community.regular', rule: 'community.regularRule' },
    { id: 'returning', label: 'community.returning', rule: 'community.returningRule' },
    { id: 'occasional', label: 'community.occasional', rule: 'community.occasionalRule' }
  ] satisfies { id: string; label: TranslationKey; rule: TranslationKey }[];
  readonly transitionLabels: Record<string, TranslationKey> = {
    enteredCore: 'community.joinedCore', restoredCore: 'community.rejoinedCore', leftCore: 'community.leftCore',
    becameRegular: 'community.becameRegular', returned: 'community.returned'
  };
  readonly memberTransitionLabels: Record<string, TranslationKey> = {
    enteredCore: 'community.joinedCore', restoredCore: 'community.rejoinedCore', leftCore: 'community.leftCore',
    becameRegular: 'community.becameRegular', returned: 'community.returned'
  };
  constructor() {
    effect(() => { this.locale.language(); untracked(() => this.drawChart()); });
    afterNextRender(() => this.subscriptions.add(this.route.queryParamMap.pipe(
      map(params => params.get('channelId')), distinctUntilChanged()
    ).subscribe(id => this.openChannel(id))));
  }

  private openChannel(id: string | null): void {
    clearTimeout(this.poll);
    this.reportRequest?.unsubscribe(); this.memberRequest?.unsubscribe(); this.exclusionsRequest?.unsubscribe();
    this.channelRequests.unsubscribe(); this.channelRequests = new Subscription();
    this.chart?.destroy(); this.chart = undefined;
    this.channelId = id && id !== this.ownerId ? id : null;
    this.responseReadOnly = false;
    this.report = null; this.members = []; this.exclusions = []; this.nextCursor = null;
    this.selectedLevel = 'core'; this.timeZoneSynced = false; this.reported = false;
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
    if (!this.report && report.quality === 'insufficient') this.selectedLevel = '';
    this.report = report;
    this.loading = false;
    if (report.recalculating) {
      this.memberRequest?.unsubscribe();
      this.membersLoading = false;
      this.members = []; this.nextCursor = null;
      this.poll = setTimeout(() => this.load(true), 5000);
    } else {
      if (changed || this.members.length === 0) this.loadMembers();
      this.poll = setTimeout(() => this.load(true), 60000);
      if (!this.readOnly && report.calculatedAt && !this.reported) {
        this.reported = true;
        this.analytics.trackEvent('community_report_open', { quality: report.quality, window_days: report.windowDays });
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
  selectLevel(level: string): void {
    this.selectedLevel = level;
    if (!this.readOnly) this.analytics.trackEvent('community_level_select', { level });
    this.loadMembers();
  }
  loadMembers(more = false): void {
    this.memberRequest?.unsubscribe();
    this.membersLoading = true;
    if (!more) { this.members = []; this.nextCursor = null; }
    this.memberRequest = this.api.members(this.selectedLevel, more ? this.nextCursor : null, this.channelId).subscribe({
      next: response => {
        this.members = more ? [...this.members, ...response.members] : response.members;
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
    if (!this.readOnly && (event.target as HTMLDetailsElement).open) this.analytics.trackEvent('community_history_open', { level: this.selectedLevel });
  }
  memberChange(member: CommunityMember): string {
    if (member.dormant) return this.locale.t('community.inactive');
    if (member.declining) return this.locale.t('community.lessActive');
    const key = member.transition ? this.memberTransitionLabels[member.transition] : undefined;
    const transition = key ? this.locale.t(key) : '';
    return member.returned && member.transition !== 'returned' ? this.locale.t('community.returned') + (transition ? ' · ' + transition : '') : transition || '—';
  }
  format(value: number | null | undefined): string { return value == null ? '—' : this.locale.number(value, 0); }
  hasTrend(): boolean { return !!this.report?.trend.some(point => point.core !== null); }
  calculatedLabel(): string {
    return this.report?.calculatedAt ? new Intl.DateTimeFormat(this.locale.locale(), {
      timeZone: this.report.timeZoneId, dateStyle: 'short', timeStyle: 'short'
    }).format(new Date(this.report.calculatedAt)) : this.locale.t('community.notCalculated');
  }
  private drawChart(): void {
    this.chart?.destroy(); this.chart = undefined;
    if (!this.trend || !this.report?.trend.length || this.report.recalculating) return;
    this.chart = new Chart(this.trend.nativeElement, {
      type: 'line', data: {
        labels: this.report.trend.map(p => new Date(p.date + 'T00:00:00Z').toLocaleDateString(this.locale.locale(), { month: 'short', day: 'numeric', timeZone: 'UTC' })),
        datasets: [{ label: this.locale.t('community.coreMembers'), data: this.report.trend.map(p => p.core),
          borderColor: '#bca0f3', backgroundColor: '#bca0f31a', fill: true, tension: .15, spanGaps: false, pointRadius: 2 }]
      }, options: {
        responsive: true, maintainAspectRatio: false, animation: false, locale: this.locale.locale(),
        plugins: { legend: { display: false } },
        scales: {
          x: { ticks: { color: '#bcb8c6', maxTicksLimit: 5 }, grid: { display: false } },
          y: { beginAtZero: true, ticks: { color: '#bcb8c6', precision: 0 }, grid: { color: '#302a3c' } }
        }
      }
    });
  }
  ngOnDestroy(): void {
    this.destroyed = true; clearTimeout(this.poll); this.reportRequest?.unsubscribe(); this.memberRequest?.unsubscribe(); this.exclusionsRequest?.unsubscribe(); this.channelRequests.unsubscribe(); this.subscriptions.unsubscribe(); this.chart?.destroy();
  }
}
