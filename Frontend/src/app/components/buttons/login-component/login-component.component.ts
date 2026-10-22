import { LocaleService } from '../../../i18n/locale.service';
import { TranslatePipe } from '../../../i18n/translate.pipe';
import { Component, inject, Input } from '@angular/core';
import { AuthService } from '../../../auth/auth.service';

@Component({
  selector: 'app-login-component',
  imports: [TranslatePipe, ],
  templateUrl: './login-component.component.html',
  styleUrl: './login-component.component.scss'
})
export class LoginComponentComponent {
  readonly locale = inject(LocaleService);
  @Input() compact = false;
  private authService = inject(AuthService);

  loginWithTwitch() {
    this.authService.loginWithTwitch();
  }
}
