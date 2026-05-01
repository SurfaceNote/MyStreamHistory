import { AfterViewInit, ChangeDetectorRef, Component, ElementRef, inject, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { StreamerService } from '../../service/streamer.service';
import { StreamerShortDTO } from '../../models/streamer-short.dto';
import { Subscription } from 'rxjs';
import { StreamSession } from '../../models/stream-session.model';
import { ViewerStats } from '../../models/viewer-stats.model';
import { CommonModule } from '@angular/common';
import { SocialLink } from '../../models/social-link.model';
import { StreamerDashboardPeriod, StreamerStatistics, TimeSeriesPoint } from '../../models/streamer-statistics.model';
import { PlaythroughStatistics } from '../../models/playthrough-statistics.model';
import { Chart, registerables } from 'chart.js';

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
  styleUrl: './streamer-profile.component.scss'
})
export class StreamerProfileComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('streamedHoursChart') streamedHoursChart?: ElementRef<HTMLCanvasElement>;
  @ViewChild('topGamesChart') topGamesChart?: ElementRef<HTMLCanvasElement>;

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
    { value: '90d', label: '90 days' }
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

  ngOnInit(): void {
    this.routeSub = this.route.paramMap.subscribe(params => {
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

  get playthroughSections(): PlaythroughStatusSection[] {
    const playthroughs = this.statistics?.playthroughs ?? [];

    const sections: Array<{ status: string; title: string }> = [
      { status: 'Playing', title: 'Playing Now' },
      { status: 'Planned', title: 'Will Play' },
      { status: 'Dropped', title: 'Dropped' },
      { status: 'Completed', title: 'Completed' }
    ];

    return sections
      .map(section => ({
        ...section,
        items: playthroughs.filter(playthrough => playthrough.status === section.status)
      }))
      .filter(section => section.items.length > 0);
  }

  ngAfterViewInit(): void {
    this.renderDashboardCharts();
  }

  get dashboardSummaryCards(): DashboardSummaryCard[] {
    const dashboard = this.statistics?.dashboard;
    const streamedDays = (dashboard?.streamedHoursByDay ?? []).filter(point => point.value > 0).length;
    const topCategory = dashboard?.topCategories?.[0];

    return [
      {
        label: 'Hours Live',
        value: `${this.formatNumber(dashboard?.totalStreamedHours ?? 0)} h`,
        hint: this.selectedPeriodLabel
      },
      {
        label: 'Streams Run',
        value: `${dashboard?.totalStreamsCount ?? 0}`,
        hint: `${streamedDays} live days`
      },
      {
        label: 'Games Covered',
        value: `${dashboard?.totalUniqueGamesCount ?? 0}`,
        hint: topCategory ? `Lead: ${topCategory.name}` : 'No categories yet'
      }
    ];
  }

  get selectedPeriodLabel(): string {
    return this.dashboardPeriods.find(period => period.value === this.selectedDashboardPeriod)?.label ?? '30 days';
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
      }
    });
  }

  getSocialLinkUrl(type: string): string | null {
    const link = this.socialLinks.find(l => l.socialNetworkType === type);
    return link ? link.fullUrl : null;
  }

  ngOnDestroy(): void {
    if (this.routeSub) {
      this.routeSub.unsubscribe();
    }

    this.destroyDashboardCharts();
  }

  loadStreamer(): void {
    this.streamerService.getStreamerByTwitchId(this.twitchId).subscribe({
      next: (data: StreamerShortDTO) => {
        this.streamerShortDTO = data;
      },
      error: (err) => {
        console.error('Error loading streamer', err);
      }
    });
  }

  loadRecentStreams(): void {
    this.isLoadingStreams = true;
    this.streamerService.getRecentStreams(this.twitchId, 10).subscribe({
      next: (data: StreamSession[]) => {
        this.recentStreams = data;
        this.isLoadingStreams = false;
      },
      error: (err) => {
        console.error('Error loading recent streams', err);
        this.isLoadingStreams = false;
      }
    });
  }

  formatDate(dateString: string): string {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', { day: '2-digit', month: '2-digit', year: 'numeric' });
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
      }
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

  loadStatistics(): void {
    this.isLoadingStatistics = true;
    this.streamerService.getStreamerStatistics(this.twitchId, this.selectedDashboardPeriod).subscribe({
      next: (data: StreamerStatistics) => {
        this.statistics = data;
        this.isLoadingStatistics = false;
        this.changeDetector.detectChanges();
        this.scheduleDashboardRender();
      },
      error: (err) => {
        console.error('Error loading statistics', err);
        this.isLoadingStatistics = false;
      }
    });
  }

  getCategoryBoxArtForStats(boxArtUrl: string, width: number, height: number): string {
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
    return (series ?? []).some(point => point.value > 0);
  }

  hasSeriesPoints(series?: TimeSeriesPoint[]): boolean {
    return (series ?? []).length > 0;
  }

  formatNumber(value: number): string {
    return Number.isInteger(value) ? value.toString() : value.toFixed(1);
  }

  formatSeriesDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
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

    if (this.streamedHoursChart && this.hasSeriesPoints(dashboard.streamedHoursByDay)) {
      this.charts.push(this.createLineChart(
        this.streamedHoursChart.nativeElement,
        dashboard.streamedHoursByDay,
        'Streamed hours',
        '#ff4d5e'
      ));
    }

    if (this.topGamesChart && dashboard.topCategories.length > 0) {
      this.charts.push(new Chart(this.topGamesChart.nativeElement, {
        type: 'doughnut',
        data: {
          labels: dashboard.topCategories.map(category => category.name),
          datasets: [{
            data: dashboard.topCategories.map(category => category.totalHours),
            backgroundColor: ['#ff4d5e', '#7c8cff', '#2dd4bf', '#f9b44a', '#a78bfa'],
            borderColor: '#171821',
            borderWidth: 3,
            hoverOffset: 8
          }]
        },
        plugins: [this.createGameMixLabelsPlugin(dashboard.topCategories)],
        options: this.baseChartOptions({
          interaction: {
            mode: 'nearest',
            intersect: true
          },
          hover: {
            mode: 'nearest',
            intersect: true
          },
          cutout: '58%',
          plugins: {
            legend: {
              display: false
            },
            tooltip: {
              enabled: false,
              external: (context: any) => this.renderGameMixTooltip(context, dashboard.topCategories),
              callbacks: {
                label: (context: any) => {
                  const label = context.label ? `${context.label}: ` : '';
                  return `${label}${this.formatNumber(context.parsed)} h`;
                }
              }
            }
          }
        })
      }));
    }
  }

  private createLineChart(canvas: HTMLCanvasElement, series: TimeSeriesPoint[], label: string, color: string): Chart {
    return new Chart(canvas, {
      type: 'line',
      data: {
        labels: series.map(point => this.formatSeriesDate(point.date)),
        datasets: [{
          label,
          data: series.map(point => point.value),
          borderColor: color,
          backgroundColor: `${color}24`,
          borderWidth: 3,
          pointRadius: 0,
          pointHoverRadius: 4,
          fill: true,
          tension: 0.35
        }]
      },
      options: this.baseCartesianOptions()
    });
  }

  private baseCartesianOptions(): any {
    return this.baseChartOptions({
      scales: {
        x: {
          grid: { display: false },
          ticks: {
            color: '#8d8da3',
            maxTicksLimit: 7
          }
        },
        y: {
          beginAtZero: true,
          grid: { color: 'rgba(255, 255, 255, 0.08)' },
          ticks: {
            color: '#8d8da3',
            precision: 0
          }
        }
      },
      plugins: {
        legend: { display: false }
      }
    });
  }

  private baseChartOptions(overrides: any = {}): any {
    const overridePlugins = overrides.plugins ?? {};

    return {
      responsive: true,
      maintainAspectRatio: false,
      interaction: {
        intersect: false,
        mode: 'index'
      },
      ...overrides,
      plugins: {
        tooltip: {
          backgroundColor: '#11121a',
          borderColor: 'rgba(255, 255, 255, 0.12)',
          borderWidth: 1,
          titleColor: '#ffffff',
          bodyColor: '#c7c7d4'
        },
        ...overridePlugins
      }
    };
  }

  private destroyDashboardCharts(): void {
    this.charts.forEach(chart => chart.destroy());
    this.charts = [];
  }

  private createGameMixLabelsPlugin(categories: Array<{ totalHours: number }>): any {
    return {
      id: 'gameMixLabels',
      afterDatasetsDraw: (chart: Chart) => {
        const { ctx } = chart;
        const meta = chart.getDatasetMeta(0);

        ctx.save();
        ctx.font = '700 12px Montserrat, sans-serif';
        ctx.fillStyle = '#f4f4f8';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';

        meta.data.forEach((arc: any, index: number) => {
          const value = categories[index]?.totalHours ?? 0;
          if (value <= 0) {
            return;
          }

          const position = arc.tooltipPosition();
          ctx.fillText(`${this.formatNumber(value)} h`, position.x, position.y);
        });

        ctx.restore();
      }
    };
  }

  private renderGameMixTooltip(context: any, categories: Array<{ name: string; boxArtUrl: string; totalHours: number; streamsCount: number }>): void {
    const { chart, tooltip } = context;
    const tooltipElement = this.getOrCreateGameMixTooltip(chart.canvas.parentNode as HTMLElement);

    if (tooltip.opacity === 0) {
      tooltipElement.style.opacity = '0';
      return;
    }

    const dataPoint = tooltip.dataPoints?.[0];
    const category = categories[dataPoint?.dataIndex ?? 0];
    if (!category) {
      tooltipElement.style.opacity = '0';
      return;
    }

    tooltipElement.replaceChildren();

    const image = document.createElement('img');
    image.src = this.getCategoryBoxArtForStats(category.boxArtUrl, 80, 106);
    image.alt = '';
    Object.assign(image.style, {
      width: '2.4rem',
      aspectRatio: '80 / 106',
      flexShrink: '0',
      borderRadius: '0.25rem',
      objectFit: 'cover'
    });

    const meta = document.createElement('div');
    const title = document.createElement('strong');
    const hours = document.createElement('span');
    const streams = document.createElement('small');

    Object.assign(meta.style, {
      display: 'flex',
      minWidth: '0',
      flexDirection: 'column',
      gap: '0.12rem'
    });

    Object.assign(title.style, {
      color: '#f4f4f8',
      fontSize: '0.82rem',
      fontWeight: '800',
      lineHeight: '1.2'
    });

    Object.assign(hours.style, {
      color: '#ffffff',
      fontSize: '0.78rem',
      fontWeight: '800'
    });

    Object.assign(streams.style, {
      color: '#8d8da3',
      fontSize: '0.72rem',
      fontWeight: '600'
    });

    title.textContent = category.name;
    hours.textContent = `${this.formatNumber(category.totalHours)} h`;
    streams.textContent = `${category.streamsCount} streams`;

    meta.append(title, hours, streams);
    tooltipElement.append(image, meta);

    const container = chart.canvas.parentNode as HTMLElement;
    const canvasRect = chart.canvas.getBoundingClientRect();
    const containerRect = container.getBoundingClientRect();
    const mousePosition = tooltip._eventPosition ?? { x: tooltip.caretX, y: tooltip.caretY };

    tooltipElement.style.opacity = '1';
    tooltipElement.style.left = `${canvasRect.left - containerRect.left + mousePosition.x + 14}px`;
    tooltipElement.style.top = `${canvasRect.top - containerRect.top + mousePosition.y + 14}px`;
  }

  private getOrCreateGameMixTooltip(container: HTMLElement): HTMLDivElement {
    let tooltipElement = container.querySelector<HTMLDivElement>('.game-mix-tooltip');

    if (!tooltipElement) {
      tooltipElement = document.createElement('div');
      tooltipElement.className = 'game-mix-tooltip';
      Object.assign(tooltipElement.style, {
        position: 'absolute',
        zIndex: '5',
        display: 'flex',
        gap: '0.65rem',
        alignItems: 'center',
        maxWidth: '15rem',
        padding: '0.55rem',
        border: '1px solid rgba(255, 255, 255, 0.12)',
        borderRadius: '0.5rem',
        background: '#11121a',
        boxShadow: '0 12px 30px rgba(0, 0, 0, 0.35)',
        opacity: '0',
        pointerEvents: 'none',
        transition: 'opacity 0.12s ease'
      });
      container.appendChild(tooltipElement);
    }

    return tooltipElement;
  }

}
