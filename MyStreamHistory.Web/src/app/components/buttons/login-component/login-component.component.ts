import { Component } from '@angular/core';

@Component({
  selector: 'app-login-component',
  imports: [],
  templateUrl: './login-component.component.html',
  styleUrl: './login-component.component.scss'
})
export class LoginComponentComponent {
  loginWithTwitch() {
    const clientId = "a77bf3umj99gay4n0ng8k5u70qsqja";
    const redirectUri = "https://localhost:5000/auth/twitch/callback";
    const scope = 'user:read:email';
    const state = this.generateRandomState();

    const twitchAuthUrl = `https://id.twitch.tv/oauth2/authorize?client_id=${clientId}&redirect_uri=${redirectUri}&response_type=code&scope=${scope}&state=${state}`;

    localStorage.setItem('twitchAuthState', state);

    window.location.href = twitchAuthUrl;
  }

  private generateRandomState(): string {
    return Math.random().toString(36).substring(2);
  }
}
