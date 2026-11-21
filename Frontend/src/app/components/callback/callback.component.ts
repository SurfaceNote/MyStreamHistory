import { TranslatePipe } from '../../i18n/translate.pipe';
import { LocaleService } from '../../i18n/locale.service';
import { afterNextRender, Component, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { switchMap, take, of } from 'rxjs';
import { AuthService } from '../../auth/auth.service';
import { AnalyticsService } from '../../service/analytics.service';

@Component({
  selector: 'app-callback',
  imports: [TranslatePipe, ],
  templateUrl: './callback.component.html',
  styleUrl: './callback.component.scss'
})
export class CallbackComponent {
  readonly locale = inject(LocaleService);
  private authService = inject(AuthService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private analytics = inject(AnalyticsService);

  constructor() { afterNextRender(() => this.completeSignIn()); }
  private completeSignIn(): void {
    const returnUrl = this.authService.consumeReturnUrl();
    this.route.queryParams.pipe(
      take(1),
      switchMap(params => params['code'] && params['state']
        ? this.authService.handleTwitchCallback(params['code'], params['state']) : of(null))
    ).subscribe(result => {
      const twitchId = this.authService.getTwitchIdFromToken();
      // A first sign-in has nothing to return to yet: show the new channel its own profile.
      const destination = this.authService.isNewChannel() && twitchId
        ? this.locale.path(['/profile', twitchId])
        : returnUrl;
      // Analytics starts only once the URL with the one-time code is gone, so the landing page is the first page view.
      void this.router.navigateByUrl(destination).finally(() => {
        this.analytics.start();
        if (result) this.analytics.trackEvent(result.isNewUser ? 'sign_up' : 'login', { method: 'Twitch' });
      });
    });
  }
}
