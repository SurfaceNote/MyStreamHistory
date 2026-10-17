import { afterNextRender, ChangeDetectorRef, Component, ElementRef, inject, OnDestroy, ViewChild } from '@angular/core';
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
  selector: 'app-community', imports: [CommonModule, FormsModule],
  templateUrl: './community.component.html', styleUrl: './community.component.scss'
})
export class CommunityComponent implements OnDestroy {
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
  error = '';
  settingsError = '';
  readonly levels = [
    { id: 'core', label: 'Core', rule: 'At least 60% attendance, four days across three weeks.' },
    { id: 'regular', label: 'Regular', rule: 'At least 30% attendance, three days across two weeks. Excludes core members.' },
    { id: 'returning', label: 'Returning', rule: 'At least two broadcast days.' },
    { id: 'occasional', label: 'Occasional', rule: 'One broadcast day.' }
  ];
  readonly transitionLabels: Record<string, string> = {
    enteredCore: 'Joined core', restoredCore: 'Rejoined core', leftCore: 'Left core',
    becameRegular: 'Became regular', returned: 'Returned'
  };
  readonly memberTransitionLabels: Record<string, string> = {
    enteredCore: 'Joined core', restoredCore: 'Rejoined core', leftCore: 'Left core',
    becameRegular: 'Became regular', returned: 'Returned'
  };
  constructor() {
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
        error: () => { this.channelsRequested = false; this.settingsError = 'Could not load channels.'; this.cdr.markForCheck(); }
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
      error: () => { this.loading = false; this.error = 'Could not load community.'; this.poll = setTimeout(() => this.load(true), 60000); this.cdr.markForCheck(); }
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
            this.settingsError = 'Could not sync your time zone. Reload to retry.';
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
      error: () => { this.membersLoading = false; this.error = 'Could not load members. Try again.'; this.cdr.markForCheck(); }
    });
  }
  loadExclusions(): void {
    this.exclusionsRequest?.unsubscribe();
    this.exclusionsRequest = this.api.exclusions(this.channelId).subscribe({
      next: r => { this.exclusions = r.exclusions; this.cdr.markForCheck(); },
      error: () => { this.settingsError = 'Could not load excluded accounts.'; this.cdr.markForCheck(); }
    });
  }
  exclusion(id: string, restore: boolean): void {
    if (this.readOnly) return;
    this.reportRequest?.unsubscribe(); clearTimeout(this.poll);
    this.saving = true; this.settingsError = '';
    this.channelRequests.add((restore ? this.api.restore(id) : this.api.exclude(id)).subscribe({
      next: r => { this.saving = false; this.apply(r.report); this.loadExclusions(); },
      error: () => { this.saving = false; this.settingsError = 'Could not update exclusions. Try again.'; this.cdr.markForCheck(); }
    }));
  }
  opened(event: Event): void {
    if (!this.readOnly && (event.target as HTMLDetailsElement).open) this.analytics.trackEvent('community_history_open', { level: this.selectedLevel });
  }
  memberChange(member: CommunityMember): string {
    if (member.dormant) return 'Inactive';
    if (member.declining) return 'Less active';
    const transition = member.transition ? this.memberTransitionLabels[member.transition] || '' : '';
    return member.returned && member.transition !== 'returned' ? 'Returned' + (transition ? ' · ' + transition : '') : transition || '—';
  }
  format(value: number | null | undefined): string { return value == null ? '—' : value.toLocaleString('en-US'); }
  hasTrend(): boolean { return !!this.report?.trend.some(point => point.core !== null); }
  calculatedLabel(): string {
    return this.report?.calculatedAt ? new Intl.DateTimeFormat('en-US', {
      timeZone: this.report.timeZoneId, dateStyle: 'short', timeStyle: 'short'
    }).format(new Date(this.report.calculatedAt)) : 'not calculated yet';
  }
  private drawChart(): void {
    this.chart?.destroy(); this.chart = undefined;
    if (!this.trend || !this.report?.trend.length || this.report.recalculating) return;
    this.chart = new Chart(this.trend.nativeElement, {
      type: 'line', data: {
        labels: this.report.trend.map(p => new Date(p.date + 'T00:00:00Z').toLocaleDateString('en-US', { month: 'short', day: 'numeric', timeZone: 'UTC' })),
        datasets: [{ label: 'Core members', data: this.report.trend.map(p => p.core),
          borderColor: '#bca0f3', backgroundColor: '#bca0f31a', fill: true, tension: .15, spanGaps: false, pointRadius: 2 }]
      }, options: {
        responsive: true, maintainAspectRatio: false, animation: false,
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
