import { LocaleService } from '../../i18n/locale.service';
import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { CommonModule } from '@angular/common';
import { SettingsSidebarComponent } from './components/settings-sidebar/settings-sidebar.component';
import { ProfileSettingsComponent } from './components/profile-settings/profile-settings.component';
import { PlaythroughSettingsComponent } from './components/playthrough-settings/playthrough-settings.component';
import { OverlaySettingsComponent } from './components/overlay-settings/overlay-settings.component';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, SettingsSidebarComponent, ProfileSettingsComponent, PlaythroughSettingsComponent, OverlaySettingsComponent],
  templateUrl: './settings.component.html',
  styleUrls: ['./settings.component.scss']
})
export class SettingsComponent {
  readonly locale = inject(LocaleService);
  private static readonly sections = ['profile', 'playthroughs', 'overlay'];
  private readonly route = inject(ActivatedRoute);
  // Links such as /settings?section=overlay open the requested section directly.
  activeSection: string = SettingsComponent.initialSection(this.route.snapshot?.queryParamMap?.get('section'));

  private static initialSection(section: string | null | undefined): string {
    return section && SettingsComponent.sections.includes(section) ? section : 'profile';
  }

  onSectionChange(section: string): void {
    this.activeSection = section;
  }
}
