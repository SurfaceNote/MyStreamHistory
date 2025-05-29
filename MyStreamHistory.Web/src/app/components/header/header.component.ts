import { Component, inject, OnDestroy, OnInit } from '@angular/core';
import { LoginComponentComponent } from '../buttons/login-component/login-component.component';
import { AuthService } from '../../auth/auth.service';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { Router } from '@angular/router';

@Component({
  selector: 'app-header',
  imports: [CommonModule, LoginComponentComponent],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss'
})
export class HeaderComponent implements OnInit, OnDestroy {
  isLoggedIn: boolean = false;
  private authService = inject(AuthService);
  private router = inject(Router);
  username: string | null = null;
  private subscriptions: Subscription = new Subscription();

  ngOnInit(): void {
    this.isLoggedIn = this.authService.isLoggedIn();
    this.username = this.authService.getUsernameFromToken();

    this.subscriptions.add(
      this.authService.getAccessTokenObservable().subscribe(token => {
        this.isLoggedIn = !!token;
      })
    );

    this.subscriptions.add(
      this.authService.getUsernameObservable().subscribe(username => {
        this.username = username;
      })
    );
  }

  ngOnDestroy(): void {
      this.subscriptions.unsubscribe();
  }

  logout() {
    this.authService.logout();
  }
}
