import { Component, inject, OnDestroy, OnInit } from '@angular/core';
import { StreamerListType } from '../../enums/streamer-list-type.enum';
import { AuthService } from '../../auth/auth.service';
import { CommonModule } from '@angular/common';
import { Subscription, timer, switchMap, from, mergeMap, map, catchError, of } from 'rxjs';
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
  
  isLoggedIn: boolean = false;
  username: string = '';
  twitchId: string = '';
  
  streamers: StreamerShortDTO[] = [];
  liveStreamerIds = new Set<number>();
  streamersLoadFailed = false;
  showingRecentStreamers = false;
  isLoadingStreamers: boolean = true;
  
  private subscriptions: Subscription = new Subscription();

  ngOnInit(): void {
    this.isLoggedIn = this.authService.isLoggedIn();
    
    if (this.isLoggedIn) {
      this.username = this.authService.getUsernameFromToken() || '';
      this.twitchId = this.authService.getTwitchIdFromToken() || '';
    }

    this.subscriptions.add(
      this.authService.getAccessTokenObservable().subscribe(token => {
        this.isLoggedIn = !!token;
        if (this.isLoggedIn) {
          this.username = this.authService.getUsernameFromToken() || '';
          this.twitchId = this.authService.getTwitchIdFromToken() || '';
        }
      })
    );

    // Load the full public directory
    this.loadStreamers();
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
          this.streamers = streamers;
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
    this.subscriptions.add(timer(0, 60000).pipe(
      switchMap(() => from(this.streamers).pipe(
        mergeMap(streamer => this.streamerService.getRecentStreams(streamer.twitchId, 1).pipe(
          map(streams => ({ id: streamer.twitchId, live: streams.some(stream => stream.isLive) })),
          catchError(() => of({ id: streamer.twitchId, live: false }))
        ), 4)
      ))
    ).subscribe(({ id, live }) => {
      if (live) this.liveStreamerIds.add(id);
      else this.liveStreamerIds.delete(id);
    }));
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }
}
