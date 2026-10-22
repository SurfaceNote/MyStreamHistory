import { TranslationKey } from '../../../../i18n/translations';
import { TranslatePipe } from '../../../../i18n/translate.pipe';
import { LocaleService, LocalizedMessage } from '../../../../i18n/locale.service';
import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SettingsService } from '../../../../service/settings.service';
import { SocialLink, SocialNetworkType } from '../../../../models/social-link.model';

interface SocialNetworkConfig {
  type: SocialNetworkType;
  label: string;
  icon: string;
  domain: string;
  placeholder: TranslationKey;
  readonly?: boolean;
}

interface SocialLinkState extends SocialLink {
  isEditing: boolean;
  error?: LocalizedMessage;
  isSaving?: boolean;
  originalPath?: string;
}

@Component({
  selector: 'app-profile-settings',
  standalone: true,
  imports: [TranslatePipe, CommonModule, FormsModule],
  templateUrl: './profile-settings.component.html',
  styleUrls: ['./profile-settings.component.scss']
})
export class ProfileSettingsComponent implements OnInit {
  readonly locale = inject(LocaleService);
  socialNetworks: SocialNetworkConfig[] = [
    { type: SocialNetworkType.YouTube, label: 'YouTube', icon: 'fa-youtube', domain: 'youtube.com/', placeholder: 'settings.youtubePath' },
    { type: SocialNetworkType.Instagram, label: 'Instagram', icon: 'fa-instagram', domain: 'instagram.com/', placeholder: 'settings.username' },
    { type: SocialNetworkType.Discord, label: 'Discord', icon: 'fa-discord', domain: 'discord.gg/', placeholder: 'settings.inviteCode' },
    { type: SocialNetworkType.Steam, label: 'Steam', icon: 'fa-steam', domain: 'steamcommunity.com/', placeholder: 'settings.steamPath' },
    { type: SocialNetworkType.VK, label: 'VK', icon: 'fa-vk', domain: 'vk.com/', placeholder: 'settings.username' },
    { type: SocialNetworkType.Yandex, label: 'Yandex Dzen', icon: 'fa-yandex', domain: 'dzen.ru/', placeholder: 'settings.username' },
    { type: SocialNetworkType.Telegram, label: 'Telegram', icon: 'fa-telegram', domain: 't.me/', placeholder: 'settings.telegramPath' }
  ];

  socialLinks: Map<string, SocialLinkState> = new Map();
  isLoading = false;
  private _loadError: LocalizedMessage = '';
  get loadError(): string { return this.locale.message(this._loadError); }
  set loadError(value: LocalizedMessage) { this._loadError = value; }
  private _globalSuccessMessage: LocalizedMessage = '';
  get globalSuccessMessage(): string { return this.locale.message(this._globalSuccessMessage); }
  set globalSuccessMessage(value: LocalizedMessage) { this._globalSuccessMessage = value; }

  constructor(private settingsService: SettingsService) {}

  ngOnInit(): void {
    this.loadSocialLinks();
  }

  loadSocialLinks(): void {
    this.isLoading = true;
    this.loadError = '';
    this.settingsService.getSocialLinks().subscribe({
      next: (response) => {
        response.socialLinks.forEach(link => {
          this.socialLinks.set(link.socialNetworkType, {
            ...link,
            isEditing: false,
            isSaving: false
          });
        });
        this.isLoading = false;
      },
      error: (error) => {
        console.error('Failed to load social links', error);
        this.loadError = 'settings.loadLinksFailed';
        this.isLoading = false;
      }
    });
  }

  getLink(type: SocialNetworkType): SocialLinkState | undefined {
    return this.socialLinks.get(type);
  }

  isReadonly(config: SocialNetworkConfig): boolean {
    return config.readonly || false;
  }

  startEditing(type: SocialNetworkType): void {
    // Cancel any other editing links first
    this.cancelAllEditing();
    
    const link = this.socialLinks.get(type);
    if (link) {
      link.originalPath = link.path; // Save original value
      link.isEditing = true;
      link.error = undefined;
    }
  }

  cancelEditing(type: SocialNetworkType): void {
    const link = this.socialLinks.get(type);
    if (link) {
      // Restore original value if it exists
      if (link.originalPath !== undefined) {
        link.path = link.originalPath;
        link.originalPath = undefined;
      }
      
      link.isEditing = false;
      link.error = undefined;
      
      // If it was a new link being added (no original path and empty), remove it
      if (!link.path || !link.path.trim()) {
        this.socialLinks.delete(type);
      }
    }
  }

  cancelAllEditing(): void {
    this.socialLinks.forEach((link, type) => {
      if (link.isEditing) {
        // Restore original value if it exists
        if (link.originalPath !== undefined) {
          link.path = link.originalPath;
          link.originalPath = undefined;
        }
        
        link.isEditing = false;
        link.error = undefined;
        
        // If it was a new link being added (no original path and empty), remove it
        if (!link.path || !link.path.trim()) {
          this.socialLinks.delete(type);
        }
      }
    });
  }

  startAdding(type: SocialNetworkType): void {
    // Cancel any other editing links first
    this.cancelAllEditing();
    
    this.socialLinks.set(type, {
      socialNetworkType: type,
      path: '',
      fullUrl: '',
      isEditing: true,
      isSaving: false
    });
  }

  saveLink(type: SocialNetworkType): void {
    const link = this.socialLinks.get(type);
    if (!link || !link.path.trim()) {
      if (link) {
        link.error = 'settings.pathRequired';
      }
      return;
    }

    link.isSaving = true;
    link.error = undefined;
    this.globalSuccessMessage = '';

    const linkToSave = {
      socialNetworkType: link.socialNetworkType,
      path: link.path.trim(),
      fullUrl: link.fullUrl
    };

    this.settingsService.updateSocialLinks({ socialLink: linkToSave }).subscribe({
      next: (response) => {
        if (response.success) {
          link.isEditing = false;
          link.isSaving = false;
          link.originalPath = undefined;
          this.globalSuccessMessage = 'settings.saved';
          setTimeout(() => this.globalSuccessMessage = '', 3000);
          this.loadSocialLinks();
        } else {
          // Response returned but with error
          const isNewLink = !link.originalPath || link.originalPath === '';
          
          if (isNewLink) {
            // For new links, clear the field on error
            link.path = '';
          } else {
            // For existing links, restore original value
            link.path = link.originalPath || '';
            link.originalPath = undefined;
          }
          
          link.error = 'settings.saveLinkFailed';
          link.isSaving = false;
          // Keep isEditing = true for new links, so user can try again
        }
      },
      error: (err) => {
        console.error('Failed to save social links', err);
        
        const isNewLink = !link.originalPath || link.originalPath === '';
        
        if (isNewLink) {
          // For new links, clear the field on error
          link.path = '';
        } else {
          // For existing links, restore original value
          link.path = link.originalPath || '';
          link.originalPath = undefined;
        }
        
        link.error = 'settings.saveLinkFailed';
        link.isSaving = false;
        // Keep isEditing = true for new links, so user can try again
        if (!isNewLink) {
          link.isEditing = false;
        }
      }
    });
  }

  deleteLink(type: SocialNetworkType): void {
    if (!confirm(this.locale.t('settings.deleteLinkConfirm'))) {
      return;
    }

    const link = this.socialLinks.get(type);
    if (link) {
      link.isSaving = true;
      link.error = undefined;
    }

    this.globalSuccessMessage = '';

    // Send empty path to signal deletion
    const linkToDelete = {
      socialNetworkType: type,
      path: '',
      fullUrl: ''
    };

    this.settingsService.updateSocialLinks({ socialLink: linkToDelete }).subscribe({
      next: (response) => {
        if (response.success) {
          this.socialLinks.delete(type);
          this.globalSuccessMessage = 'settings.linkDeleted';
          setTimeout(() => this.globalSuccessMessage = '', 3000);
        } else {
          // Restore the link if deletion failed
          if (link) {
            link.error = 'settings.deleteLinkFailed';
            link.isSaving = false;
          }
        }
      },
      error: (err) => {
        console.error('Failed to delete social link', err);
        // Restore the link if deletion failed
        if (link) {
          link.error = 'settings.deleteLinkFailed';
          link.isSaving = false;
        }
      }
    });
  }
}
