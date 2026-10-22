import { TranslationKey } from '../../../../i18n/translations';
import { TranslatePipe } from '../../../../i18n/translate.pipe';
import { LocaleService } from '../../../../i18n/locale.service';
import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';

interface SidebarSection {
  id: string;
  label: TranslationKey;
  icon: string;
  disabled?: boolean;
}

@Component({
  selector: 'app-settings-sidebar',
  standalone: true,
  imports: [TranslatePipe, CommonModule],
  templateUrl: './settings-sidebar.component.html',
  styleUrls: ['./settings-sidebar.component.scss']
})
export class SettingsSidebarComponent {
  readonly locale = inject(LocaleService);
  @Input() activeSection: string = 'profile';
  @Output() sectionChange = new EventEmitter<string>();

  sections: SidebarSection[] = [
    { id: 'profile', label: 'ui.profile', icon: 'fa-user' },
    { id: 'playthroughs', label: 'settings.playthroughs', icon: 'fa-folder-open' },
    { id: 'overlay', label: 'settings.overlay', icon: 'fa-trophy' },
    { id: 'content', label: 'settings.content', icon: 'fa-video', disabled: true },
    { id: 'streams', label: 'ui.streamsLabel', icon: 'fa-broadcast-tower', disabled: true }
  ];

  selectSection(sectionId: string, disabled?: boolean): void {
    if (!disabled) {
      this.sectionChange.emit(sectionId);
    }
  }
}
