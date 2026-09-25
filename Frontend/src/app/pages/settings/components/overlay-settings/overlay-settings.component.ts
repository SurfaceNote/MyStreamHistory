import { Component, inject } from '@angular/core';
import { AuthService } from '../../../../auth/auth.service';

@Component({
  selector: 'app-overlay-settings',
  standalone: true,
  templateUrl: './overlay-settings.component.html',
  styleUrl: './overlay-settings.component.scss'
})
export class OverlaySettingsComponent {
  private auth = inject(AuthService);
  copyStatus = '';

  get overlayUrl(): string | null {
    const twitchId = this.auth.getTwitchIdFromToken();
    return twitchId && /^\d+$/.test(twitchId)
      ? `${window.location.origin}/overlay/top-viewers/${twitchId}`
      : null;
  }

  async copyLink(): Promise<void> {
    const url = this.overlayUrl;
    if (!url) return;

    try {
      await navigator.clipboard.writeText(url);
      this.copyStatus = 'Link copied';
    } catch {
      this.copyStatus = 'Could not copy automatically. Select and copy the link above.';
    }
  }
}
