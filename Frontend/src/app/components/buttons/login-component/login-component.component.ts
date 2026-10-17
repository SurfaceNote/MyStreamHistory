import { Component, inject, Input } from '@angular/core';
import { AuthService } from '../../../auth/auth.service';

@Component({
  selector: 'app-login-component',
  imports: [],
  templateUrl: './login-component.component.html',
  styleUrl: './login-component.component.scss'
})
export class LoginComponentComponent {
  @Input() compact = false;
  private authService = inject(AuthService);

  loginWithTwitch() {
    this.authService.loginWithTwitch();
  }
}
