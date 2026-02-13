import { Component, inject, OnDestroy, OnInit } from '@angular/core';
import { StreamerListType } from '../../enums/streamer-list-type.enum';
import { AuthService } from '../../auth/auth.service';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
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

    // Load list of new streamers
    this.loadStreamers();
  }

  private loadStreamers(): void {
    this.isLoadingStreamers = true;
    this.subscriptions.add(
      this.streamerService.getStreamers(StreamerListType.NewStreamers).subscribe({
        next: (streamers) => {
          this.streamers = streamers;
          this.isLoadingStreamers = false;
        },
        error: (error) => {
          console.error('Error loading streamers:', error);
          this.isLoadingStreamers = false;
          this.streamers = [];
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }
}
