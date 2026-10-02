import { afterNextRender, ChangeDetectorRef, Component, inject, OnDestroy } from '@angular/core';
import { LoginComponentComponent } from '../buttons/login-component/login-component.component';
import { AuthService } from '../../auth/auth.service';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { Router, RouterModule } from '@angular/router';

@Component({
  selector: 'app-header',
  imports: [CommonModule, LoginComponentComponent, RouterModule],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss'
})
export class HeaderComponent implements OnDestroy {
  isLoggedIn: boolean = false;
  authReady = false;
  private authService = inject(AuthService);
  private router = inject(Router);
  private changeDetector = inject(ChangeDetectorRef);
  username: string | null = null;
  twitchId: string | null = null;
  isAdmin: boolean = false;
  private subscriptions: Subscription = new Subscription();

  constructor() {
    afterNextRender(() => this.initializeAuth());
  }

  private initializeAuth(): void {
    this.subscriptions.add(
      this.authService.getAccessTokenObservable().subscribe(token => {
        this.isLoggedIn = !!token;
        this.authReady = true;
        if (this.isLoggedIn) {
          this.username = this.authService.getUsernameFromToken();
          this.twitchId = this.authService.getTwitchIdFromToken();
          this.isAdmin = this.authService.isAdmin();
        } else {
          this.username = null;
          this.twitchId = null;
          this.isAdmin = false;
        }
        this.changeDetector.markForCheck();
      })
    );

    this.subscriptions.add(
      this.authService.getUsernameObservable().subscribe(username => {
        this.username = username;
        this.changeDetector.markForCheck();
      })
    );
    this.changeDetector.detectChanges();
  }

  ngOnDestroy(): void {
      this.subscriptions.unsubscribe();
  }

  logout() {
    this.authService.logout();
  }

  navigateToSettings() {
    this.router.navigate(['/settings']);
  }

  navigateToAdmin() {
    this.router.navigate(['/admin/publications']);
  }
}
