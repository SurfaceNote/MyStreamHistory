import { Component } from '@angular/core';
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
  activeSection: string = 'profile';

  onSectionChange(section: string): void {
    this.activeSection = section;
  }
}
