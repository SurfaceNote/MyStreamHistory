import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Subscription } from 'rxjs';
import { ViewerStats } from '../../models/viewer-stats.model';
import { StreamerService } from '../../service/streamer.service';

interface OverlayRow {
  key: string;
  viewer: ViewerStats;
  position: number;
  entering: boolean;
  leaving: boolean;
  gainedExperience: number | null;
  newcomer: boolean;
}

@Component({
  selector: 'app-top-viewers-overlay',
  standalone: true,
  templateUrl: './top-viewers-overlay.component.html',
  styleUrl: './top-viewers-overlay.component.scss'
})
export class TopViewersOverlayComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly streamerService = inject(StreamerService);
  private readonly cycleMs = 10 * 60 * 1000;
  private readonly holdMs = 5 * 1000;
  private readonly animationMs = 700;
  private readonly timers = new Set<ReturnType<typeof setTimeout>>();
  private refreshInterval?: ReturnType<typeof setInterval>;
  private request?: Subscription;
  private twitchId?: number;
  private previousTop: ViewerStats[] | null = null;
  private fetching = false;

  rows: OverlayRow[] = [];
  visible = false;

  ngOnInit(): void {
    document.documentElement.classList.add('obs-overlay');
    document.body.classList.add('obs-overlay');

    const id = this.route.snapshot.paramMap.get('twitchId');
    if (!id || !/^\d+$/.test(id)) return;
    this.twitchId = Number(id);
    if (!Number.isSafeInteger(this.twitchId) || this.twitchId <= 0) return;

    this.showCycle();
    this.refreshInterval = setInterval(() => this.showCycle(), this.cycleMs);
  }

  ngOnDestroy(): void {
    document.documentElement.classList.remove('obs-overlay');
    document.body.classList.remove('obs-overlay');
    if (this.refreshInterval) clearInterval(this.refreshInterval);
    for (const timer of this.timers) clearTimeout(timer);
    this.request?.unsubscribe();
  }

  private schedule(callback: () => void, delay: number): void {
    const timer = setTimeout(() => {
      this.timers.delete(timer);
      callback();
    }, delay);
    this.timers.add(timer);
  }

  private showCycle(): void {
    if (!this.twitchId || this.fetching) return;
    this.fetching = true;
    this.request = this.streamerService.getTopViewers(this.twitchId, 100).subscribe({
      next: (latest) => {
        this.fetching = false;
        const nextTop = latest.slice(0, 10);
        const previousTop = this.previousTop;
        const oldTop = previousTop?.slice(0, 10) ?? nextTop;
        this.previousTop = latest;

        if (!oldTop.length && !nextTop.length) return;

        this.rows = oldTop.map((viewer, position) => this.toRow(viewer, position));
        this.visible = true;

        this.schedule(() => this.animateRanking(nextTop, previousTop), this.holdMs);
        this.schedule(() => {
          this.visible = false;
          this.schedule(() => { this.rows = []; }, this.animationMs);
        }, this.holdMs * 2 + this.animationMs);
      },
      error: (error) => {
        this.fetching = false;
        console.error('Failed to load OBS top viewers', error);
      }
    });
  }

  private animateRanking(nextTop: ViewerStats[], previousTop: ViewerStats[] | null): void {
    const oldRows = new Map(this.rows.map(row => [row.key, row]));
    const nextKeys = new Set(nextTop.map(viewer => this.viewerKey(viewer)));
    const previousByViewer = new Map(previousTop?.map(viewer => [this.viewerKey(viewer), viewer]) ?? []);

    this.rows = [
      ...nextTop.map((viewer, position) => {
        const existing = oldRows.get(this.viewerKey(viewer));
        const previous = previousByViewer.get(this.viewerKey(viewer));
        return {
          ...this.toRow(viewer, position),
          entering: !existing,
          gainedExperience: previous ? viewer.experience - previous.experience : null,
          newcomer: previousTop !== null && !previous
        };
      }),
      ...this.rows.filter(row => !nextKeys.has(row.key)).map(row => ({ ...row, entering: false, leaving: true }))
    ];

    this.schedule(() => {
      this.rows = this.rows.filter(row => !row.leaving);
    }, this.animationMs);
  }

  private toRow(viewer: ViewerStats, position: number): OverlayRow {
    return {
      key: this.viewerKey(viewer), viewer, position,
      entering: true, leaving: false, gainedExperience: null, newcomer: false
    };
  }

  private viewerKey(viewer: ViewerStats): string {
    return viewer.viewer?.twitchUserId || viewer.viewerId || viewer.id;
  }

  formatExperience(experience: number): string {
    return Math.round(experience).toLocaleString('en-US');
  }

  formatGain(gain: number): string {
    const rounded = Math.round(gain * 100) / 100;
    return `${rounded >= 0 ? '+' : ''}${rounded.toLocaleString('en-US', { maximumFractionDigits: 2 })} XP`;
  }
}
