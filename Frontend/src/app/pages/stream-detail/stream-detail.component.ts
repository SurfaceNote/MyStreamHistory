import { Component, inject, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { StreamerService } from '../../service/streamer.service';
import { StreamDetails, CategoryDetails, StreamViewer } from '../../models/stream-details.model';

@Component({
  selector: 'app-stream-detail',
  imports: [CommonModule],
  templateUrl: './stream-detail.component.html',
  styleUrl: './stream-detail.component.scss'
})
export class StreamDetailComponent implements OnInit, OnDestroy {
  streamId!: string;
  streamDetails: StreamDetails | null = null;
  isLoading: boolean = false;
  error: string | null = null;
  private routeSub: Subscription | null = null;

  private streamerService = inject(StreamerService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

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
    if (this.routeSub) {
      this.routeSub.unsubscribe();
    }
  }

  loadStreamDetails(): void {
    this.isLoading = true;
    this.error = null;
    
    this.streamerService.getStreamDetails(this.streamId).subscribe({
      next: (data: StreamDetails) => {
        this.streamDetails = data;
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Error loading stream details', err);
        this.error = 'Failed to load stream details';
        this.isLoading = false;
      }
    });
  }

  navigateToStreamerProfile(): void {
    if (this.streamDetails) {
      this.router.navigate(['/profile', this.streamDetails.twitchUserId]);
    }
  }

  formatDate(date: Date): string {
    const d = new Date(date);
    return d.toLocaleDateString('en-US', { 
      day: '2-digit', 
      month: 'long', 
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  formatDateShort(date: Date): string {
    const d = new Date(date);
    return d.toLocaleDateString('en-US', { 
      month: 'short', 
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
    
    if (hours > 0) {
      return `${hours}h ${minutes}m`;
    }
    return `${minutes}m`;
  }

  formatMinutes(minutes: number): string {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    
    if (hours > 0) {
      return `${hours}h ${mins}m`;
    }
    return `${mins}m`;
  }

  formatHours(minutes: number): string {
    const hours = (minutes / 60).toFixed(2);
    return hours;
  }

  formatPoints(points: number): string {
    return points.toFixed(2);
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
