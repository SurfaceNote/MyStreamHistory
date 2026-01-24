import { HttpParams } from '@angular/common/http';
import { Component, inject, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { StreamerService } from '../../service/streamer.service';
import { StreamerShortDTO } from '../../models/streamer-short.dto';
import { Subscription } from 'rxjs';
import { StreamSession } from '../../models/stream-session.model';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-streamer-profile',
  imports: [CommonModule],
  templateUrl: './streamer-profile.component.html',
  styleUrl: './streamer-profile.component.scss'
})
export class StreamerProfileComponent implements OnInit, OnDestroy {
  twitchId!: number;
  streamerShortDTO!: StreamerShortDTO;
  recentStreams: StreamSession[] = [];
  isLoadingStreams: boolean = false;
  private routeSub: Subscription | null = null;

  private streamerService = inject(StreamerService)
  private route = inject(ActivatedRoute);

  ngOnInit(): void {
    this.routeSub = this.route.paramMap.subscribe(params => {
      const idParam = params.get('twitchId');
      if (idParam) {
        this.twitchId = +idParam;
        this.loadStreamer();
        this.loadRecentStreams();
      }
    });
  }

  ngOnDestroy(): void {
    if (this.routeSub) {
      this.routeSub.unsubscribe();
    }
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
}
