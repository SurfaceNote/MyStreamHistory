import { afterNextRender, ChangeDetectorRef, Component, inject, OnDestroy, OnInit, PLATFORM_ID } from '@angular/core';
import { StreamerListType } from '../../enums/streamer-list-type.enum';
import { AuthService } from '../../auth/auth.service';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Subscription, timer, exhaustMap, catchError, EMPTY } from 'rxjs';
import { RouterLink } from '@angular/router';
import { LoginComponentComponent } from '../../components/buttons/login-component/login-component.component';
import { StreamerService } from '../../service/streamer.service';
import { StreamerShortDTO } from '../../models/streamer-short.dto';

@Component({
  selector: 'app-home',
  imports: [CommonModule, RouterLink, LoginComponentComponent],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent implements OnInit, OnDestroy {
  StreamerListType = StreamerListType;

  private authService = inject(AuthService);
  private streamerService = inject(StreamerService);
  private platformId = inject(PLATFORM_ID);
  private changeDetector = inject(ChangeDetectorRef);
  
  isLoggedIn: boolean = false;
  authReady = false;
  username: string = '';
  twitchId: string = '';
  
  streamers: StreamerShortDTO[] = [];
  liveStreamerIds = new Set<number>();
  streamersLoadFailed = false;
  showingRecentStreamers = false;
  isLoadingStreamers: boolean = true;
  
  private subscriptions: Subscription = new Subscription();

  constructor() {
    // Keep the initial browser view identical to SSR until hydration has finished.
    afterNextRender(() => this.initializeAuth());
  }

  ngOnInit(): void {
    this.loadStreamers();
  }

  private initializeAuth(): void {
    this.subscriptions.add(
      this.authService.getAccessTokenObservable().subscribe(token => {
        this.isLoggedIn = !!token;
        this.authReady = true;
        if (this.isLoggedIn) {
          this.username = this.authService.getUsernameFromToken() || '';
          this.twitchId = this.authService.getTwitchIdFromToken() || '';
        } else {
          this.username = '';
          this.twitchId = '';
        }
        this.changeDetector.markForCheck();
      })
    );
    this.changeDetector.detectChanges();
  }

  private loadStreamers(): void {
    this.isLoadingStreamers = true;
    this.subscriptions.add(
      this.streamerService.getAllStreamers().pipe(
        catchError(() => {
          // Older API deployments do not expose the full directory yet.
          this.showingRecentStreamers = true;
          return this.streamerService.getStreamers(StreamerListType.NewStreamers);
        })
      ).subscribe({
        next: (streamers) => {
          this.updateStreamers(streamers);
          this.watchLiveStatus();
          this.isLoadingStreamers = false;
        },
        error: (error) => {
          this.streamersLoadFailed = true;
          console.error('Error loading streamers:', error);
          this.isLoadingStreamers = false;
          this.streamers = [];
        }
      })
    );
  }

  private watchLiveStatus(): void {
    if (!isPlatformBrowser(this.platformId)) return;

    this.subscriptions.add(timer(60000, 60000).pipe(
      exhaustMap(() => this.streamerService.getAllStreamers().pipe(
        // Keep the last successful snapshot and retry on the next tick.
        catchError(() => EMPTY)
      ))
    ).subscribe(streamers => {
      this.updateStreamers(streamers);
      this.showingRecentStreamers = false;
    }));
  }

  private updateStreamers(streamers: StreamerShortDTO[]): void {
    this.streamers = streamers;
    this.liveStreamerIds = new Set(
      streamers.filter(streamer => streamer.isLive).map(streamer => streamer.twitchId)
    );
    this.changeDetector.markForCheck();
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }
}
