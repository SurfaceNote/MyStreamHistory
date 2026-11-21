import { LocaleService } from '../../../i18n/locale.service';
import { TranslatePipe } from '../../../i18n/translate.pipe';
import { Component, inject, Input } from '@angular/core';
import { AuthService } from '../../../auth/auth.service';
import { AnalyticsService } from '../../../service/analytics.service';

@Component({
  selector: 'app-login-component',
  imports: [TranslatePipe, ],
  templateUrl: './login-component.component.html',
  styleUrl: './login-component.component.scss'
})
export class LoginComponentComponent {
  readonly locale = inject(LocaleService);
  @Input() compact = false;
  /** Where the button is shown, reported with `login_start`. */
  @Input() placement = 'unknown';
  private authService = inject(AuthService);
  private analytics = inject(AnalyticsService);
  private leaving = false;

  async loginWithTwitch(): Promise<void> {
    if (this.leaving) return;
    this.leaving = true;
    // The Twitch page replaces this one, so the event is given a moment to leave first.
    await this.analytics.trackEventBeforeLeaving('login_start', { location: this.placement });
    this.authService.loginWithTwitch();
    // A page restored from the back/forward cache keeps this state: the button must work again.
    this.leaving = false;
  }
}
