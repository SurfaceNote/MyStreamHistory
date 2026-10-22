import { TranslatePipe } from '../../../../i18n/translate.pipe';
import { LocaleService, LocalizedMessage } from '../../../../i18n/locale.service';
import { Component, inject } from '@angular/core';
import { AuthService } from '../../../../auth/auth.service';

@Component({
  imports: [TranslatePipe],
  selector: 'app-overlay-settings',
  standalone: true,
  templateUrl: './overlay-settings.component.html',
  styleUrl: './overlay-settings.component.scss'
})
export class OverlaySettingsComponent {
  readonly locale = inject(LocaleService);
  private auth = inject(AuthService);
  private _copyStatus: LocalizedMessage = '';
  get copyStatus(): string { return this.locale.message(this._copyStatus); }
  set copyStatus(value: LocalizedMessage) { this._copyStatus = value; }

  get overlayUrl(): string | null {
    const twitchId = this.auth.getTwitchIdFromToken();
    return twitchId && /^\d+$/.test(twitchId)
      ? `${window.location.origin}/overlay/top-viewers/${twitchId}?language=${this.locale.language()}`
      : null;
  }

  async copyLink(): Promise<void> {
    const url = this.overlayUrl;
    if (!url) return;

    try {
      await navigator.clipboard.writeText(url);
      this.copyStatus = 'settings.linkCopied';
    } catch {
      this.copyStatus = 'settings.copyFailed';
    }
  }
}
