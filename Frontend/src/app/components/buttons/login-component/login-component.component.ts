import { Component, inject } from '@angular/core';
import { AuthService } from '../../../auth/auth.service';

@Component({
  selector: 'app-login-component',
  imports: [],
  templateUrl: './login-component.component.html',
  styleUrl: './login-component.component.scss'
})
export class LoginComponentComponent {
  private authService = inject(AuthService);

  loginWithTwitch() {
    this.authService.loginWithTwitch();
  }
}
