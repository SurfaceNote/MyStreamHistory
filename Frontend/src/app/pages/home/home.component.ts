import { Component, inject, OnDestroy, OnInit } from '@angular/core';
import { SmallListBlockStreamersComponent } from "../../components/blocks/small-list-block-streamers/small-list-block-streamers.component";
import { StreamerListType } from '../../enums/streamer-list-type.enum';
import { AuthService } from '../../auth/auth.service';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-home',
  imports: [CommonModule, SmallListBlockStreamersComponent],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent implements OnInit, OnDestroy {
  StreamerListType = StreamerListType;

  private authService = inject(AuthService);
  isLoggedIn: boolean = false;
  private subscriptions: Subscription = new Subscription();

  ngOnInit(): void {
    this.isLoggedIn = this.authService.isLoggedIn();

    this.subscriptions.add(
      this.authService.getAccessTokenObservable().subscribe(token => {
        this.isLoggedIn = !!token;
      })
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }
}
