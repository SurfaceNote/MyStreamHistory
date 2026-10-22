import { TranslatePipe } from '../../i18n/translate.pipe';
import { LocaleService } from '../../i18n/locale.service';
import { afterNextRender, Component, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { switchMap, take, of } from 'rxjs';
import { AuthService } from '../../auth/auth.service';

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

  constructor() { afterNextRender(() => this.completeSignIn()); }
  private completeSignIn(): void {
    const returnUrl = this.authService.consumeReturnUrl();
    this.route.queryParams.pipe(
      take(1),
      switchMap(params => params['code'] && params['state']
        ? this.authService.handleTwitchCallback(params['code'], params['state']) : of(false))
    ).subscribe(() => void this.router.navigateByUrl(returnUrl));
  }
}
