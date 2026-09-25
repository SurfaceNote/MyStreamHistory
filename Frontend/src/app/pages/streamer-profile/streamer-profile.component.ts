import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  ElementRef,
  inject,
  OnDestroy,
  OnInit,
  ViewChild,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { StreamerService } from '../../service/streamer.service';
import { StreamerShortDTO } from '../../models/streamer-short.dto';
import {
  catchError,
  finalize,
  forkJoin,
  map,
  of,
  Subscription,
  switchMap,
} from 'rxjs';
import { StreamSession } from '../../models/stream-session.model';
import { ViewerStats } from '../../models/viewer-stats.model';
import { CommonModule } from '@angular/common';
import { SocialLink } from '../../models/social-link.model';
import {
  StreamerDashboardPeriod,
  StreamerStatistics,
  TimeSeriesPoint,
} from '../../models/streamer-statistics.model';
import { PlaythroughStatistics } from '../../models/playthrough-statistics.model';
import { Chart, registerables } from 'chart.js';
import { SeoService } from '../../service/seo.service';

Chart.register(...registerables);

interface PlaythroughStatusSection {
  status: string;
  title: string;
  items: PlaythroughStatistics[];
}

interface DashboardSummaryCard {
  label: string;
  value: string;
  hint: string;
}

@Component({
  selector: 'app-streamer-profile',
  imports: [CommonModule],
  templateUrl: './streamer-profile.component.html',
  styleUrl: './streamer-profile.component.scss',
})
export class StreamerProfileComponent
  implements OnInit, AfterViewInit, OnDestroy
{
  @ViewChild('streamedHoursChart')
  streamedHoursChart?: ElementRef<HTMLCanvasElement>;

  readonly socialNetworks = [
    { name: 'YouTube', icon: 'youtube', color: '#e00022' },
    { name: 'Discord', icon: 'discord', color: '#5865f2' },
    { name: 'Instagram', icon: 'instagram', color: '#c13584' },
    { name: 'Steam', icon: 'steam', color: '#23445a' },
    { name: 'VK', icon: 'vk', color: '#0077ff' },
    { name: 'Yandex', icon: 'yandex', color: '#d93025' },
    { name: 'Telegram', icon: 'telegram', color: '#229ed9' },
  ];
  profileLoadFailed = false;
  activeSection = 'Overview';
  sections = ['Overview', 'Streams', 'Games', 'Viewers'];
  gameStatus = 'All';
  readonly gameStatuses = [
    { value: 'All', label: 'All Games' },
    { value: 'Playing', label: 'Playing Now' },
    { value: 'Completed', label: 'Completed' },
    { value: 'Planned', label: 'Planned' },
    { value: 'Dropped', label: 'Dropped' },
  ];
  gameQuery = '';
  gameSort = 'hours';

  get featuredStream(): StreamSession | undefined {
    return (
      this.recentStreams.find((stream) => stream.isLive) ??
      this.recentStreams[0]
    );
  }

  get filteredGames() {
    const query = this.gameQuery.trim().toLowerCase();
    return [...(this.statistics?.categories ?? [])]
      .filter((game) => game.name.toLowerCase().includes(query))
      .sort((a, b) =>
        this.gameSort === 'name'
          ? a.name.localeCompare(b.name)
          : b.totalHours - a.totalHours,
      );
  }

  selectSection(section: string): void {
    this.activeSection = section;
    if (section === 'Overview') this.scheduleDashboardRender();
  }

  gameShare(hours: number): number {
    const maximum = Math.max(
      ...(this.statistics?.dashboard.topCategories ?? []).map(
        (game) => game.totalHours,
      ),
      1,
    );
    return (hours / maximum) * 100;
  }

  twitchId!: number;
  streamerShortDTO!: StreamerShortDTO;
  recentStreams: StreamSession[] = [];
  topViewers: ViewerStats[] = [];
  socialLinks: SocialLink[] = [];
  statistics: StreamerStatistics | null = null;
  selectedDashboardPeriod: StreamerDashboardPeriod = '30d';
  dashboardPeriods: Array<{ value: StreamerDashboardPeriod; label: string }> = [
    { value: '7d', label: '7 days' },
    { value: '30d', label: '30 days' },
    { value: '90d', label: '90 days' },
  ];
  isLoadingStreams: boolean = false;
  isLoadingViewers: boolean = false;
  isLoadingSocialLinks: boolean = false;
  isLoadingStatistics: boolean = false;
  private routeSub: Subscription | null = null;
  private charts: Chart[] = [];

  private streamerService = inject(StreamerService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private changeDetector = inject(ChangeDetectorRef);
  private seo = inject(SeoService);

  ngOnInit(): void {
    this.routeSub = this.route.paramMap.subscribe((params) => {
      const idParam = params.get('twitchId');
      if (idParam) {
        this.twitchId = +idParam;
        this.loadStreamer();
        this.loadRecentStreams();
        this.loadTopViewers();
        this.loadSocialLinks();
        this.loadStatistics();
      }
    });
  }

  get overviewPlaythroughSections(): PlaythroughStatusSection[] {
    return this.playthroughSections.filter((section) =>
      ['Playing', 'Completed'].includes(section.status),
    );
  }

  get selectedPlaythroughs(): PlaythroughStatistics[] {
    const query = this.gameQuery.trim().toLowerCase();
    return (this.statistics?.playthroughs ?? [])
      .filter(
        (game) =>
          game.status === this.gameStatus &&
          game.gameName.toLowerCase().includes(query),
      )
      .sort((a, b) =>
        this.gameSort === 'name'
          ? a.gameName.localeCompare(b.gameName)
          : b.totalHours - a.totalHours,
      );
  }

  get playthroughSections(): PlaythroughStatusSection[] {
    const playthroughs = this.statistics?.playthroughs ?? [];

    const sections: Array<{ status: string; title: string }> = [
      { status: 'Playing', title: 'Playing Now' },
      { status: 'Planned', title: 'Will Play' },
      { status: 'Dropped', title: 'Dropped' },
      { status: 'Completed', title: 'Completed' },
    ];

    return sections
      .map((section) => ({
        ...section,
        items: playthroughs.filter(
          (playthrough) => playthrough.status === section.status,
        ),
      }))
      .filter((section) => section.items.length > 0);
  }

  ngAfterViewInit(): void {
    this.renderDashboardCharts();
  }

  get dashboardSummaryCards(): DashboardSummaryCard[] {
    const dashboard = this.statistics?.dashboard;
    const streamedDays = (dashboard?.streamedHoursByDay ?? []).filter(
      (point) => point.value > 0,
    ).length;
    const topCategory = dashboard?.topCategories?.[0];

    return [
      {
        label: 'Hours Live',
        value: `${this.formatNumber(dashboard?.totalStreamedHours ?? 0)} h`,
        hint: this.selectedPeriodLabel,
      },
      {
        label: 'Streams',
        value: `${dashboard?.totalStreamsCount ?? 0}`,
        hint: `${streamedDays} live days`,
      },
      {
        label: 'Games played',
        value: `${dashboard?.totalUniqueGamesCount ?? 0}`,
        hint: topCategory ? `Lead: ${topCategory.name}` : 'No categories yet',
      },
    ];
  }

  get selectedPeriodLabel(): string {
    return (
      this.dashboardPeriods.find(
        (period) => period.value === this.selectedDashboardPeriod,
      )?.label ?? '30 days'
    );
  }

  loadSocialLinks(): void {
    this.isLoadingSocialLinks = true;
    this.streamerService.getSocialLinksByTwitchId(this.twitchId).subscribe({
      next: (response) => {
        this.socialLinks = response.socialLinks;
        this.isLoadingSocialLinks = false;
      },
      error: (err) => {
        console.error('Error loading social links', err);
        this.isLoadingSocialLinks = false;
      },
    });
  }

  getSocialLinkUrl(type: string): string | null {
    const link = this.socialLinks.find((l) => l.socialNetworkType === type);
    return link ? link.fullUrl : null;
  }

  ngOnDestroy(): void {
    if (this.routeSub) {
      this.routeSub.unsubscribe();
    }

    this.destroyDashboardCharts();
  }

  loadStreamer(): void {
    this.profileLoadFailed = false;
    this.streamerService.getStreamerByTwitchId(this.twitchId).subscribe({
      next: (data: StreamerShortDTO) => {
        this.streamerShortDTO = data;
        this.seo.update({
          title: `${data.displayName} — Twitch Stream Stats | MyStreamHistory`,
          description: `Explore ${data.displayName}'s Twitch stream history, games, audience activity and channel performance.`,
          image: data.avatar,
          type: 'profile',
        });
      },
      error: (err) => {
        this.profileLoadFailed = true;
        console.error('Error loading streamer', err);
      },
    });
  }

  loadRecentStreams(): void {
    this.isLoadingStreams = true;
    this.streamerService
      .getRecentStreams(this.twitchId, 10)
      .pipe(
        switchMap((streams: StreamSession[]) => {
          if (streams.length === 0) {
            return of([] as StreamSession[]);
          }

          return forkJoin(
            streams.map((stream) =>
              this.streamerService.getStreamDetails(stream.id).pipe(
                map((details) => ({
                  ...stream,
                  uniqueViewersCount: details.viewers.length,
                })),
                catchError((err) => {
                  console.error(
                    `Error loading unique viewers for stream ${stream.id}`,
                    err,
                  );
                  return of({ ...stream, uniqueViewersCount: 0 });
                }),
              ),
            ),
          );
        }),
        finalize(() => {
          this.isLoadingStreams = false;
        }),
      )
      .subscribe({
        next: (data: StreamSession[]) => {
          this.recentStreams = data;
        },
        error: (err) => {
          console.error('Error loading recent streams', err);
        },
      });
  }

  formatDate(dateString: string): string {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
    });
  }

  formatDuration(startedAt: string, endedAt?: string): string {
    const start = new Date(startedAt);
    const end = endedAt ? new Date(endedAt) : new Date();
    const durationMs = end.getTime() - start.getTime();
    const hours = Math.floor(durationMs / (1000 * 60 * 60));
    const minutes = Math.floor((durationMs % (1000 * 60 * 60)) / (1000 * 60));

    if (hours > 0) {
      return `${hours}h ${minutes}m`;
    }
    return `${minutes}m`;
  }

  getCategoryBoxArt(boxArtUrl: string, width: number, height: number): string {
    return boxArtUrl
      .replace('{width}', width.toString())
      .replace('{height}', height.toString());
  }

  loadTopViewers(): void {
    this.isLoadingViewers = true;
    this.streamerService.getTopViewers(this.twitchId, 100).subscribe({
      next: (data: ViewerStats[]) => {
        this.topViewers = data;
        this.isLoadingViewers = false;
      },
      error: (err) => {
        console.error('Error loading top viewers', err);
        this.isLoadingViewers = false;
      },
    });
  }

  formatHours(minutes: number): string {
    const hours = Math.floor(minutes / 60);
    return `${hours} h`;
  }

  formatMessagePoints(points: number): string {
    return points.toFixed(1);
  }

  getDefaultAvatar(): string {
    return 'https://static-cdn.jtvnw.net/user-default-pictures-uv/ebe4cd89-b4f4-4cd9-adac-2f30151b4209-profile_image-70x70.png';
  }

  navigateToStreamDetail(streamId: string): void {
    this.router.navigate(['/stream', streamId]);
  }

  navigateToViewerStats(viewer: ViewerStats): void {
    const viewerTwitchId = viewer.viewer?.twitchUserId;
    if (viewerTwitchId) {
      this.router.navigate([
        '/profile',
        this.twitchId,
        'viewer',
        viewerTwitchId,
      ]);
    }
  }

  loadStatistics(): void {
    this.isLoadingStatistics = true;
    this.streamerService
      .getStreamerStatistics(this.twitchId, this.selectedDashboardPeriod)
      .subscribe({
        next: (data: StreamerStatistics) => {
          this.statistics = data;
          this.isLoadingStatistics = false;
          this.changeDetector.detectChanges();
          this.scheduleDashboardRender();
        },
        error: (err) => {
          console.error('Error loading statistics', err);
          this.isLoadingStatistics = false;
        },
      });
  }

  getCategoryBoxArtForStats(
    boxArtUrl: string,
    width: number,
    height: number,
  ): string {
    return boxArtUrl
      .replace('{width}', width.toString())
      .replace('{height}', height.toString());
  }

  selectDashboardPeriod(period: StreamerDashboardPeriod): void {
    if (this.selectedDashboardPeriod === period) {
      return;
    }

    this.selectedDashboardPeriod = period;
    this.loadStatistics();
  }

  hasSeriesData(series?: TimeSeriesPoint[]): boolean {
    return (series ?? []).some((point) => point.value > 0);
  }

  hasSeriesPoints(series?: TimeSeriesPoint[]): boolean {
    return (series ?? []).length > 0;
  }

  formatNumber(value: number): string {
    return Number.isInteger(value) ? value.toString() : value.toFixed(1);
  }

  formatSeriesDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
    });
  }

  private scheduleDashboardRender(): void {
    requestAnimationFrame(() => this.renderDashboardCharts());
  }

  private renderDashboardCharts(): void {
    const dashboard = this.statistics?.dashboard;
    if (!dashboard) {
      return;
    }

    this.destroyDashboardCharts();

    if (
      this.streamedHoursChart &&
      this.hasSeriesPoints(dashboard.streamedHoursByDay)
    ) {
      this.charts.push(
        this.createHoursChart(
          this.streamedHoursChart.nativeElement,
          dashboard.streamedHoursByDay,
          'Streamed hours',
          '#a78bfa',
        ),
      );
    }
  }

  private createHoursChart(
    canvas: HTMLCanvasElement,
    series: TimeSeriesPoint[],
    label: string,
    color: string,
  ): Chart {
    return new Chart(canvas, {
      type: 'bar',
      data: {
        labels: series.map((point) => this.formatSeriesDate(point.date)),
        datasets: [
          {
            label,
            data: series.map((point) => point.value),
            borderColor: color,
            backgroundColor: color,
            borderWidth: 0,
            borderRadius: 4,
            maxBarThickness: 22,
          },
        ],
      },
      options: this.baseCartesianOptions(),
    });
  }

  private baseCartesianOptions(): any {
    return this.baseChartOptions({
      scales: {
        x: {
          grid: { display: false },
          ticks: {
            color: '#8d8da3',
            maxTicksLimit: 7,
          },
        },
        y: {
          beginAtZero: true,
          grid: { color: 'rgba(255, 255, 255, 0.08)' },
          ticks: {
            color: '#8d8da3',
            precision: 0,
          },
        },
      },
      plugins: {
        legend: { display: false },
      },
    });
  }

  private baseChartOptions(overrides: any = {}): any {
    const overridePlugins = overrides.plugins ?? {};

    return {
      responsive: true,
      maintainAspectRatio: false,
      interaction: {
        intersect: false,
        mode: 'index',
      },
      ...overrides,
      plugins: {
        tooltip: {
          backgroundColor: '#11121a',
          borderColor: 'rgba(255, 255, 255, 0.12)',
          borderWidth: 1,
          titleColor: '#ffffff',
          bodyColor: '#c7c7d4',
        },
        ...overridePlugins,
      },
    };
  }

  private destroyDashboardCharts(): void {
    this.charts.forEach((chart) => chart.destroy());
    this.charts = [];
  }
}
