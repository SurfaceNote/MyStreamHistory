import { LocaleService } from '../../i18n/locale.service';
import { TranslatePipe } from '../../i18n/translate.pipe';
import { GameArtComponent } from '../../components/game-art/game-art.component';
import { CommonModule } from '@angular/common';
import { Component, inject, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, Subscription } from 'rxjs';
import { StreamerShortDTO } from '../../models/streamer-short.dto';
import { ViewerStats } from '../../models/viewer-stats.model';
import { StreamerService } from '../../service/streamer.service';
import { SeoService } from '../../service/seo.service';

@Component({
  selector: 'app-viewer-stats',
  imports: [TranslatePipe, GameArtComponent, CommonModule],
  templateUrl: './viewer-stats.component.html',
  styleUrl: './viewer-stats.component.scss'
})
export class ViewerStatsComponent implements OnInit, OnDestroy {
  readonly locale = inject(LocaleService);
  twitchId!: number;
  viewerTwitchId = '';
  streamer: StreamerShortDTO | null = null;
  stats: ViewerStats | null = null;
  isLoading = true;
  error: string | null = null;

  private routeSubscription: Subscription | null = null;
  private streamerService = inject(StreamerService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private seo = inject(SeoService);

  ngOnInit(): void {
    this.routeSubscription = this.route.paramMap.subscribe(params => {
      this.twitchId = Number(params.get('twitchId'));
      this.viewerTwitchId = params.get('viewerTwitchId') ?? '';
      this.loadStats();
    });
  }

  ngOnDestroy(): void {
    this.routeSubscription?.unsubscribe();
  }

  loadStats(): void {
    if (!Number.isFinite(this.twitchId) || !this.viewerTwitchId) {
      this.error = this.locale.t('ui.invalidViewerStatisticsLink');
      this.isLoading = false;
      return;
    }

    this.isLoading = true;
    this.error = null;

    forkJoin({
      streamer: this.streamerService.getStreamerByTwitchId(this.twitchId),
      stats: this.streamerService.getViewerStats(this.twitchId, this.viewerTwitchId)
    }).subscribe({
      next: result => {
        this.streamer = result.streamer;
        this.stats = result.stats;
        const viewerName = result.stats.viewer?.displayName || this.locale.t('ui.viewer');
        this.seo.update({
          title: this.locale.t('seo.viewerTitle', { viewer: viewerName, name: result.streamer.displayName }),
          description: this.locale.t('seo.viewerDescription', { name: result.streamer.displayName }),
          image: result.stats.viewer?.profileImageUrl || result.streamer.avatar,
          noIndex: true
        });
        this.isLoading = false;
      },
      error: error => {
        console.error('Error loading viewer statistics', error);
        this.error = this.locale.t('ui.viewerStatisticsAreNotAvailable');
        this.isLoading = false;
      }
    });
  }

  navigateBack(): void {
    this.router.navigateByUrl(this.locale.path(['/profile', this.twitchId]));
  }

  navigateToStream(streamSessionId: string): void {
    this.router.navigateByUrl(this.locale.path(['/stream', streamSessionId]));
  }

  formatWatchTime(minutes: number): string {
    const hours = Math.floor(minutes / 60);
    const remainingMinutes = minutes % 60;
    return this.locale.duration(minutes);
  }

  formatDate(value: string): string {
    return new Date(value).toLocaleString(this.locale.locale(), {
      timeZone: 'UTC', day: 'numeric',
      month: 'long',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  getCategoryBoxArt(boxArtUrl: string, width: number, height: number): string {
    return boxArtUrl
      .replace('{width}', width.toString())
      .replace('{height}', height.toString());
  }

  getDefaultAvatar(): string {
    return 'https://static-cdn.jtvnw.net/user-default-pictures-uv/ebe4cd89-b4f4-4cd9-adac-2f30151b4209-profile_image-70x70.png';
  }
}
