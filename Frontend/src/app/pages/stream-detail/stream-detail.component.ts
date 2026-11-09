import { ResponseStatusService } from '../../core/api/response-status.service';
import { LocaleService } from '../../i18n/locale.service';
import { TranslatePipe, LocalPathPipe } from '../../i18n/translate.pipe';
import { GameArtComponent } from '../../components/game-art/game-art.component';
import { Component, ElementRef, inject, OnDestroy, OnInit, PLATFORM_ID, ViewChild } from '@angular/core';
import { Chart, registerables } from 'chart.js';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { StreamerService } from '../../service/streamer.service';
import { StreamDetails, CategoryDetails, StreamViewer } from '../../models/stream-details.model';
import { RecapViewer, StreamRecap } from '../../models/stream-recap.model';
import { TranslationKey } from '../../i18n/translations';
import { SeoService } from '../../service/seo.service';
import { AuthService } from '../../auth/auth.service';

Chart.register(...registerables);

const NEWLINE = '\n';

interface CategorySegment {
  id: string;
  key: string;
  name: string;
  start: number;
  end: number;
  color: string;
}

@Component({
  selector: 'app-stream-detail',
  imports: [TranslatePipe, LocalPathPipe, RouterLink, GameArtComponent, CommonModule],
  templateUrl: './stream-detail.component.html',
  styleUrl: './stream-detail.component.scss'
})
export class StreamDetailComponent implements OnInit, OnDestroy {
  private readonly responseStatus = inject(ResponseStatusService);
  readonly locale = inject(LocaleService);
  streamId!: string;
  streamDetails: StreamDetails | null = null;
  isLoading: boolean = false;
  error: string | null = null;
  recap: StreamRecap | null = null;
  copyState: { kind: 'text' | 'link'; ok: boolean } | null = null;
  private copyTimer?: ReturnType<typeof setTimeout>;
  private routeSub: Subscription | null = null;
  private detailsSub?: Subscription;
  private recapSub?: Subscription;
  private chart?: Chart<'line', { x: number; y: number | null }[]>;
  private audienceCanvas?: HTMLCanvasElement;
  private platform = inject(PLATFORM_ID);
  private auth = inject(AuthService);
  @ViewChild('audienceChart') set audienceChart(element: ElementRef<HTMLCanvasElement> | undefined) {
    this.audienceCanvas = element?.nativeElement;
    this.drawAudience();
  }

  get audience() { return this.streamDetails?.audience ?? []; }
  get durationMinutes(): number {
    if (!this.streamDetails) return 0;
    const end = this.streamDetails.endedAt ? new Date(this.streamDetails.endedAt).getTime() : Date.now();
    return Math.max(0, (end - new Date(this.streamDetails.startedAt).getTime()) / 60000);
  }
  get categorySegments(): CategorySegment[] {
    if (!this.streamDetails) return [];
    const start = new Date(this.streamDetails.startedAt).getTime();
    const categories = [...this.streamDetails.categories].sort((a, b) => +new Date(a.startedAt) - +new Date(b.startedAt));
    const colors = ['#bca0f3', '#5ed6bb', '#f3bc70', '#79b9f4', '#ef8db3'];
    const assigned = new Map<string, string>();
    const segments: CategorySegment[] = [];
    categories.forEach((category, index) => {
      const from = Math.max(0, (+new Date(category.startedAt) - start) / 60000);
      const next = categories[index + 1];
      const to = Math.min(this.durationMinutes,
        next ? (+new Date(next.startedAt) - start) / 60000 : this.durationMinutes,
        category.endedAt ? (+new Date(category.endedAt) - start) / 60000 : this.durationMinutes);
      if (!Number.isFinite(from) || !Number.isFinite(to) || to <= from) return;
      const key = category.twitchId || category.name;
      if (!assigned.has(key)) assigned.set(key, colors[assigned.size % colors.length]);
      const previous = segments.at(-1);
      if (previous?.key === key && Math.abs(previous.end - from) < .0001) previous.end = to;
      else segments.push({ id: category.streamCategoryId, key, name: category.name, start: from, end: to, color: assigned.get(key)! });
    });
    return segments;
  }
  categoriesInBucket(index: number): string[] {
    const point = this.audience[index];
    if (!point || !this.streamDetails) return [];
    const start = (new Date(point.startedAt).getTime() - new Date(this.streamDetails.startedAt).getTime()) / 60000;
    return [...new Set(this.categorySegments.filter(c => c.start < start + 5 && c.end > start).map(c => c.name))];
  }
  elapsedMinutes(value: number): string {
    const seconds = Math.max(0, Math.round(value * 60));
    const label = `${Math.floor(seconds / 3600)}:${String(Math.floor(seconds / 60) % 60).padStart(2, '0')}`;
    return label + (seconds % 60 ? ':' + String(seconds % 60).padStart(2, '0') : '');
  }
  elapsed(start: string): string {
    return this.elapsedMinutes((new Date(start).getTime() - new Date(this.streamDetails!.startedAt).getTime()) / 60000);
  }
  private drawAudience(): void {
    this.chart?.destroy();
    this.chart = undefined;
    if (!isPlatformBrowser(this.platform) || !this.audienceCanvas || !this.audience.length) return;
    const segments = this.categorySegments;
    this.chart = new Chart(this.audienceCanvas, {
      type: 'line',
      data: { datasets: [{
        label: this.locale.t('ui.averageViewers'), data: this.audience.map(p => ({
          x: (new Date(p.startedAt).getTime() - new Date(this.streamDetails!.startedAt).getTime()) / 60000,
          y: p.averageViewers
        })),
        borderColor: '#a78bfa', backgroundColor: '#a78bfa22', fill: true, tension: 0, spanGaps: false, pointRadius: 3
      }] },
      plugins: [{
        id: 'categoryTimeline',
        beforeDatasetsDraw: chart => {
          const { ctx, chartArea, scales: { x } } = chart;
          ctx.save();
          for (const category of segments) {
            ctx.fillStyle = category.color + '12';
            ctx.fillRect(x.getPixelForValue(category.start), chartArea.top,
              x.getPixelForValue(category.end) - x.getPixelForValue(category.start), chartArea.bottom - chartArea.top);
          }
          ctx.restore();
        },
        afterDatasetsDraw: chart => {
          const { ctx, chartArea, scales: { x } } = chart;
          ctx.save(); ctx.setLineDash([4, 4]); ctx.lineWidth = 1;
          for (const category of segments.filter(c => c.start > 0)) {
            const position = x.getPixelForValue(category.start);
            ctx.strokeStyle = category.color;
            ctx.beginPath(); ctx.moveTo(position, chartArea.top); ctx.lineTo(position, chartArea.bottom); ctx.stroke();
          }
          ctx.restore();
        }
      }],
      options: { locale: this.locale.locale(), responsive: true, maintainAspectRatio: false, parsing: false, animation: false,
        plugins: { legend: { display: false }, tooltip: { callbacks: {
          title: items => {
            const from = items[0]?.parsed.x ?? 0;
            return `${this.elapsedMinutes(from)}–${this.elapsedMinutes(Math.min(from + 5, this.durationMinutes))}`;
          },
          afterLabel: item => this.categoriesInBucket(item.dataIndex).join(' → ')
        } } },
        scales: { y: { beginAtZero: true, ticks: { color: '#bcb8c6' }, grid: { color: '#ffffff14' } },
          x: { type: 'linear', min: 0, max: this.durationMinutes, title: { display: true, text: 'Elapsed time', color: '#bcb8c6' },
            ticks: { color: '#bcb8c6', maxTicksLimit: 8, callback: value => this.elapsedMinutes(Number(value)) }, grid: { display: false } } }
      }
    });
  }

  private streamerService = inject(StreamerService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private seo = inject(SeoService);

  ngOnInit(): void {
    this.routeSub = this.route.paramMap.subscribe(params => {
      const idParam = params.get('streamId');
      if (idParam) {
        this.streamId = idParam;
        this.loadStreamDetails();
      }
    });
  }

  ngOnDestroy(): void {
    this.detailsSub?.unsubscribe();
    this.recapSub?.unsubscribe();
    clearTimeout(this.copyTimer);
    this.chart?.destroy();
    if (this.routeSub) {
      this.routeSub.unsubscribe();
    }
  }

  loadStreamDetails(): void {
    this.isLoading = true;
    this.error = null;

    this.detailsSub?.unsubscribe();
    this.detailsSub = this.streamerService.getStreamDetails(this.streamId).subscribe({
      next: (data: StreamDetails) => {
        this.responseStatus.reset();
        this.streamDetails = data;
        this.seo.update({
          title: this.locale.t('seo.streamTitle', { date: this.formatDateShort(data.startedAt), name: data.streamerDisplayName }),
          description: this.locale.t('seo.streamDescription', { name: data.streamerDisplayName }),
          image: data.streamerAvatarUrl || undefined,
          type: 'article'
        });
        this.isLoading = false;
        this.loadRecap();
      },
      error: (err) => {
        this.responseStatus.fail(err);
        this.seo.update({ title: this.locale.t(err.status === 404 ? 'error.notFound' : 'error.unavailable'), description: this.locale.t('error.unavailable'), noIndex: true });
        console.error('Error loading stream details', err);
        this.error = this.locale.t('ui.failedToLoadStreamDetails');
        this.isLoading = false;
      }
    });
  }

  // ---- Stream recap: only the streamer sees it (and the admin panel). Loaded in the browser only,
  // because the session lives in the browser and the page is complete without it.

  loadRecap(): void {
    this.recapSub?.unsubscribe();
    this.recap = null;
    if (!isPlatformBrowser(this.platform) || !this.isOwnStream) return;
    this.recapSub = this.streamerService.getStreamRecap(this.streamId).subscribe({
      next: (recap) => { this.recap = recap; },
      error: (err) => console.error('Error loading stream recap', err),
    });
  }

  get isOwnStream(): boolean {
    return !!this.streamDetails && this.auth.getTwitchIdFromToken() === String(this.streamDetails.twitchUserId);
  }

  get hasComparison(): boolean {
    return (this.recap?.comparisonStreamsCount ?? 0) > 0;
  }

  /** "35 min longer than usual"; small differences read as usual. */
  durationDelta(recap: StreamRecap): string {
    if (recap.averageDurationMinutes == null || recap.isLive) return '';
    const delta = recap.durationMinutes - recap.averageDurationMinutes;
    if (Math.abs(delta) < Math.max(10, recap.averageDurationMinutes * 0.1)) return this.locale.t('recap.asUsual');
    return this.locale.t(delta > 0 ? 'recap.longer' : 'recap.shorter', { value: this.locale.duration(Math.abs(delta)) });
  }

  countDelta(value: number, average: number | null): string {
    if (average == null) return '';
    const delta = Math.round((value - average) * 10) / 10;
    if (Math.abs(delta) < 1) return this.locale.t('recap.asUsual');
    return this.locale.t(delta > 0 ? 'recap.moreThanUsual' : 'recap.fewerThanUsual', { value: this.locale.number(Math.abs(delta), 0) });
  }

  deltaTone(value: number, average: number | null): 'up' | 'down' | '' {
    if (average == null || Math.abs(value - average) < Math.max(1, average * 0.1)) return '';
    return value > average ? 'up' : 'down';
  }

  viewerNote(viewer: RecapViewer, kind: 'new' | 'returning' | 'streak' | 'chat'): string {
    if (kind === 'returning' && viewer.missedStreams) return this.locale.plural('missedCount', viewer.missedStreams);
    if (kind === 'streak' && viewer.streak) return this.locale.plural('streakCount', viewer.streak);
    if (kind === 'chat') return `${this.formatPoints(viewer.chatPoints)} ${this.locale.t('ui.msgPts')}`;
    return this.locale.duration(viewer.minutesWatched);
  }

  readonly playthroughStatusLabels: Record<string, TranslationKey> = { Playing: 'ui.playing', Completed: 'ui.completed', Planned: 'ui.planned', Dropped: 'ui.dropped' };

  /** Plain text for Discord or a social post. */
  recapText(recap: StreamRecap, details: StreamDetails): string {
    const names = (viewers: RecapViewer[], total: number) => {
      const shown = viewers.slice(0, 8).map((viewer) => viewer.displayName).join(', ');
      return total > 8 ? `${shown} ${this.locale.t('recap.moreCount', { count: total - 8 })}` : shown;
    };
    const lines = [
      this.locale.t('recap.shareTitle', { name: details.streamerDisplayName, date: this.formatDateShort(details.startedAt) }),
      details.streamTitle ? `“${details.streamTitle}”` : '',
      '',
      '⏱ ' + this.locale.t('recap.shareDuration', { value: this.locale.duration(recap.durationMinutes) }),
      recap.peakViewers != null ? '📈 ' + this.locale.t('recap.sharePeak', { value: recap.peakViewers }) : '',
      recap.viewerDataAvailable ? '💬 ' + this.locale.t('recap.shareChat', { value: recap.chatAccounts }) : '',
      ...recap.playthroughs.map((item) => '🎮 ' + this.locale.t('recap.sharePlaythrough', { game: item.gameName, number: item.streamNumber })),
      recap.newCount ? '✨ ' + this.locale.t('recap.shareNew', { names: names(recap.newViewers, recap.newCount) }) : '',
      recap.returningCount ? '🔁 ' + this.locale.t('recap.shareReturning', { names: names(recap.returningViewers, recap.returningCount) }) : '',
      '',
      this.shareUrl,
    ];
    return lines.filter((line, index) => line !== '' || (index > 0 && lines[index - 1] !== '')).join(NEWLINE).trim();
  }

  get shareUrl(): string {
    return isPlatformBrowser(this.platform) ? window.location.origin + window.location.pathname : '';
  }

  async copy(kind: 'text' | 'link'): Promise<void> {
    if (!this.recap || !this.streamDetails) return;
    const value = kind === 'text' ? this.recapText(this.recap, this.streamDetails) : this.shareUrl;
    let ok = true;
    try { await navigator.clipboard.writeText(value); } catch { ok = false; }
    this.copyState = { kind, ok };
    clearTimeout(this.copyTimer);
    this.copyTimer = setTimeout(() => { this.copyState = null; }, 2500);
  }

  viewerLink(viewer: RecapViewer): (string | number)[] {
    return ['/profile', this.streamDetails!.twitchUserId, 'viewer', viewer.twitchUserId];
  }

  navigateToStreamerProfile(): void {
    if (this.streamDetails) {
      this.router.navigateByUrl(this.locale.path(['/profile', this.streamDetails.twitchUserId]));
    }
  }

  navigateToViewerStats(viewer: StreamViewer): void {
    if (this.streamDetails) {
      this.router.navigateByUrl(this.locale.path([
        '/profile',
        this.streamDetails.twitchUserId,
        'viewer',
        viewer.twitchUserId
      ]));
    }
  }

  formatDate(date: Date): string {
    const d = new Date(date);
    return d.toLocaleDateString(this.locale.locale(), {
      timeZone: 'UTC', day: '2-digit',
      month: 'long',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  formatDateShort(date: Date): string {
    const d = new Date(date);
    return d.toLocaleDateString(this.locale.locale(), {
      timeZone: 'UTC', month: 'short',
      day: 'numeric',
      year: 'numeric'
    });
  }

  formatTotalMinutes(startedAt: Date, endedAt?: Date | null): number {
    const start = new Date(startedAt);
    const end = endedAt ? new Date(endedAt) : new Date();
    const durationMs = end.getTime() - start.getTime();
    return Math.floor(durationMs / (1000 * 60));
  }

  formatDuration(startedAt: Date, endedAt?: Date | null): string {
    const start = new Date(startedAt);
    const end = endedAt ? new Date(endedAt) : new Date();
    const durationMs = end.getTime() - start.getTime();
    const hours = Math.floor(durationMs / (1000 * 60 * 60));
    const minutes = Math.floor((durationMs % (1000 * 60 * 60)) / (1000 * 60));

    return this.locale.duration(hours * 60 + minutes);
  }

  formatMinutes(minutes: number): string {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;

    return this.locale.duration(minutes);
  }

  formatHours(minutes: number): string {
    const hours = (minutes / 60).toFixed(2);
    return this.locale.number(Number(hours), 2);
  }

  formatPoints(points: number): string {
    return this.locale.number(points, 2);
  }

  getCategoryBoxArt(boxArtUrl: string, width: number, height: number): string {
    return boxArtUrl
      .replace('{width}', width.toString())
      .replace('{height}', height.toString());
  }

  getTwitchProfileImage(twitchUserId: string): string {
    // Placeholder - in real app, you'd fetch this from Twitch API
    return `https://static-cdn.jtvnw.net/user-default-pictures-uv/cdd517fe-def4-11e9-948e-784f43822e80-profile_image-300x300.png`;
  }

  getTotalPoints(viewer: StreamViewer): number {
    return viewer.chatPoints + viewer.viewerPoints;
  }
}
