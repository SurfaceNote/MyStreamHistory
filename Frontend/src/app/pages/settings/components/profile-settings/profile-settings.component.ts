import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SettingsService } from '../../../../service/settings.service';
import { SocialLink, SocialNetworkType } from '../../../../models/social-link.model';

interface SocialNetworkConfig {
  type: SocialNetworkType;
  label: string;
  icon: string;
  domain: string;
  placeholder: string;
  readonly?: boolean;
}

interface SocialLinkState extends SocialLink {
  isEditing: boolean;
  error?: string;
  isSaving?: boolean;
  originalPath?: string;
}

@Component({
  selector: 'app-profile-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './profile-settings.component.html',
  styleUrls: ['./profile-settings.component.scss']
})
export class ProfileSettingsComponent implements OnInit {
  socialNetworks: SocialNetworkConfig[] = [
    { type: SocialNetworkType.YouTube, label: 'YouTube', icon: 'fa-youtube', domain: 'youtube.com/', placeholder: '@username or c/channelname' },
    { type: SocialNetworkType.Instagram, label: 'Instagram', icon: 'fa-instagram', domain: 'instagram.com/', placeholder: 'username' },
    { type: SocialNetworkType.Discord, label: 'Discord', icon: 'fa-discord', domain: 'discord.gg/', placeholder: 'invite-code' },
    { type: SocialNetworkType.Steam, label: 'Steam', icon: 'fa-steam', domain: 'steamcommunity.com/', placeholder: 'id/username' },
    { type: SocialNetworkType.VK, label: 'VK', icon: 'fa-vk', domain: 'vk.com/', placeholder: 'username' },
    { type: SocialNetworkType.Yandex, label: 'Yandex Dzen', icon: 'fa-yandex', domain: 'dzen.ru/', placeholder: 'username' },
    { type: SocialNetworkType.Telegram, label: 'Telegram', icon: 'fa-telegram', domain: 't.me/', placeholder: 'username or joinchat/code' }
  ];

  socialLinks: Map<string, SocialLinkState> = new Map();
  isLoading = false;
  globalSuccessMessage = '';

  constructor(private settingsService: SettingsService) {}

  ngOnInit(): void {
    this.loadSocialLinks();
  }

  loadSocialLinks(): void {
    this.isLoading = true;
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
        link.error = 'Path cannot be empty';
      }
      return;
    }

    link.isSaving = true;
    link.error = undefined;
    this.globalSuccessMessage = '';

    const linksToSave = Array.from(this.socialLinks.values())
      .filter(l => l.path && l.path.trim())
      .map(l => ({
        socialNetworkType: l.socialNetworkType,
        path: l.path.trim(),
        fullUrl: l.fullUrl
      }));

    this.settingsService.updateSocialLinks({ socialLinks: linksToSave }).subscribe({
      next: (response) => {
        if (response.success) {
          link.isEditing = false;
          link.isSaving = false;
          link.originalPath = undefined;
          this.globalSuccessMessage = 'Successfully saved';
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
          
          link.error = response.error || 'Failed to save link';
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
        
        // Try to extract error message from different possible locations
        let errorMessage = 'Failed to save link';
        
        if (err.error) {
          // If error is ApiResponse structure
          if (err.error.data && err.error.data.error) {
            errorMessage = err.error.data.error;
          }
          // If error is direct UpdateSocialLinksResponse
          else if (err.error.error) {
            errorMessage = err.error.error;
          }
          // If error is string
          else if (typeof err.error === 'string') {
            errorMessage = err.error;
          }
        }
        
        link.error = errorMessage;
        link.isSaving = false;
        // Keep isEditing = true for new links, so user can try again
        if (!isNewLink) {
          link.isEditing = false;
        }
      }
    });
  }

  deleteLink(type: SocialNetworkType): void {
    if (!confirm('Are you sure you want to delete this link?')) {
      return;
    }

    const link = this.socialLinks.get(type);
    if (link) {
      link.isSaving = true;
      link.error = undefined;
    }

    this.socialLinks.delete(type);
    this.globalSuccessMessage = '';

    const linksToSave = Array.from(this.socialLinks.values())
      .filter(l => l.path && l.path.trim())
      .map(l => ({
        socialNetworkType: l.socialNetworkType,
        path: l.path.trim(),
        fullUrl: l.fullUrl
      }));

    this.settingsService.updateSocialLinks({ socialLinks: linksToSave }).subscribe({
      next: (response) => {
        if (response.success) {
          this.globalSuccessMessage = 'Link successfully deleted';
          setTimeout(() => this.globalSuccessMessage = '', 3000);
        } else {
          // Restore the link if deletion failed
          if (link) {
            this.socialLinks.set(type, link);
            link.error = response.error || 'Failed to delete link';
            link.isSaving = false;
          }
        }
      },
      error: (err) => {
        console.error('Failed to delete social link', err);
        // Restore the link if deletion failed
        if (link) {
          this.socialLinks.set(type, link);
          
          // Try to extract error message from different possible locations
          let errorMessage = 'Failed to delete link';
          
          if (err.error) {
            // If error is ApiResponse structure
            if (err.error.data && err.error.data.error) {
              errorMessage = err.error.data.error;
            }
            // If error is direct UpdateSocialLinksResponse
            else if (err.error.error) {
              errorMessage = err.error.error;
            }
            // If error is string
            else if (typeof err.error === 'string') {
              errorMessage = err.error;
            }
          }
          
          link.error = errorMessage;
          link.isSaving = false;
        }
      }
    });
  }
}
