import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

interface SidebarSection {
  id: string;
  label: string;
  icon: string;
  disabled?: boolean;
}

@Component({
  selector: 'app-settings-sidebar',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './settings-sidebar.component.html',
  styleUrls: ['./settings-sidebar.component.scss']
})
export class SettingsSidebarComponent {
  @Input() activeSection: string = 'profile';
  @Output() sectionChange = new EventEmitter<string>();

  sections: SidebarSection[] = [
    { id: 'profile', label: 'Profile', icon: 'fa-user' },
    { id: 'playthroughs', label: 'Playthroughs', icon: 'fa-folder-open' },
    { id: 'content', label: 'Content', icon: 'fa-video', disabled: true },
    { id: 'streams', label: 'Streams', icon: 'fa-broadcast-tower', disabled: true }
  ];

  selectSection(sectionId: string, disabled?: boolean): void {
    if (!disabled) {
      this.sectionChange.emit(sectionId);
    }
  }
}
