import { SeoService } from '../../service/seo.service';
import { ResponseStatusService } from '../../core/api/response-status.service';
import { LocaleService } from '../../i18n/locale.service';
import { TranslatePipe, LocalPathPipe } from '../../i18n/translate.pipe';
import { afterNextRender, ChangeDetectorRef, Component, inject, OnDestroy, OnInit, PLATFORM_ID } from '@angular/core';
import { StreamerListType } from '../../enums/streamer-list-type.enum';
import { AuthService } from '../../auth/auth.service';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Subscription, timer, exhaustMap, catchError, EMPTY } from 'rxjs';
import { RouterLink } from '@angular/router';
import { LoginComponentComponent } from '../../components/buttons/login-component/login-component.component';
import { StreamerService } from '../../service/streamer.service';
import { StreamerShortDTO } from '../../models/streamer-short.dto';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-home',
  imports: [TranslatePipe, LocalPathPipe, CommonModule, FormsModule, RouterLink, LoginComponentComponent],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent implements OnInit, OnDestroy {
  private readonly seo = inject(SeoService);
  private readonly responseStatus = inject(ResponseStatusService);
  readonly locale = inject(LocaleService);
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
  searchQuery = '';
  liveOnly = false;
  private liveStatusSubscription?: Subscription;

  get filteredStreamers(): StreamerShortDTO[] {
    const query = this.searchQuery.trim().toLocaleLowerCase();
    return this.streamers
      .filter(streamer => (!this.liveOnly || this.liveStreamerIds.has(streamer.twitchId)) &&
        streamer.displayName.toLocaleLowerCase().includes(query))
      .sort((a, b) => Number(this.liveStreamerIds.has(b.twitchId)) - Number(this.liveStreamerIds.has(a.twitchId)) ||
        a.displayName.localeCompare(b.displayName));
  }

  resetFilters(): void {
    this.searchQuery = '';
    this.liveOnly = false;
  }

  retryStreamers(): void {
    if (!this.isLoadingStreamers) this.loadStreamers();
  }
  
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
    this.streamersLoadFailed = false;
    this.showingRecentStreamers = false;
    this.subscriptions.add(
      this.streamerService.getAllStreamers().pipe(
        catchError(() => {
          // Older API deployments do not expose the full directory yet.
          this.showingRecentStreamers = true;
          return this.streamerService.getStreamers(StreamerListType.NewStreamers);
        })
      ).subscribe({
        next: (streamers) => {
          this.responseStatus.reset();
          this.seo.update({ title: this.locale.t('seo.homeTitle'), description: this.locale.t('seo.homeDescription') });
          this.updateStreamers(streamers);
          this.watchLiveStatus();
          this.isLoadingStreamers = false;
          this.changeDetector.markForCheck();
        },
        error: (error) => {
          this.responseStatus.fail({ status: 502 });
          this.streamersLoadFailed = true;
          this.showingRecentStreamers = false;
          console.error('Error loading streamers:', error);
          this.isLoadingStreamers = false;
          this.streamers = [];
          this.changeDetector.markForCheck();
        }
      })
    );
  }

  private watchLiveStatus(): void {
    if (!isPlatformBrowser(this.platformId)) return;

    this.liveStatusSubscription?.unsubscribe();
    this.liveStatusSubscription = timer(60000, 60000).pipe(
      exhaustMap(() => this.streamerService.getAllStreamers().pipe(
        // Keep the last successful snapshot and retry on the next tick.
        catchError(() => EMPTY)
      ))
    ).subscribe(streamers => {
      this.updateStreamers(streamers);
      this.showingRecentStreamers = false;
    });
    this.subscriptions.add(this.liveStatusSubscription);
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
