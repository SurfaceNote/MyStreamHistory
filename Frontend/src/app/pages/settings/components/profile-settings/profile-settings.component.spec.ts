import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { ProfileSettingsComponent } from './profile-settings.component';
import { SettingsService } from '../../../../service/settings.service';
import { AnalyticsService } from '../../../../service/analytics.service';
import { SocialNetworkType } from '../../../../models/social-link.model';

describe('ProfileSettingsComponent analytics', () => {
  let component: ProfileSettingsComponent;
  let analytics: jasmine.SpyObj<AnalyticsService>;
  let settings: jasmine.SpyObj<SettingsService>;

  beforeEach(() => {
    analytics = jasmine.createSpyObj<AnalyticsService>('AnalyticsService', ['trackEvent']);
    settings = jasmine.createSpyObj<SettingsService>('SettingsService', ['getSocialLinks', 'updateSocialLinks']);
    settings.getSocialLinks.and.returnValue(of({ socialLinks: [] } as any));
    settings.updateSocialLinks.and.returnValue(of({ success: true } as any));
    TestBed.configureTestingModule({
      imports: [ProfileSettingsComponent],
      providers: [
        { provide: SettingsService, useValue: settings },
        { provide: AnalyticsService, useValue: analytics },
      ],
    });
    component = TestBed.createComponent(ProfileSettingsComponent).componentInstance;
  });

  it('tracks added and edited links with the network only', () => {
    component.startAdding(SocialNetworkType.YouTube);
    component.socialLinks.get(SocialNetworkType.YouTube)!.path = '@secret-channel';
    component.saveLink(SocialNetworkType.YouTube);
    expect(analytics.trackEvent).toHaveBeenCalledOnceWith('profile_link_save', { network: 'youtube', action: 'add' });

    analytics.trackEvent.calls.reset();
    component.socialLinks.set(SocialNetworkType.YouTube, {
      socialNetworkType: SocialNetworkType.YouTube, path: '@secret-channel', fullUrl: '', isEditing: false,
    } as any);
    component.startEditing(SocialNetworkType.YouTube);
    component.saveLink(SocialNetworkType.YouTube);
    expect(analytics.trackEvent).toHaveBeenCalledOnceWith('profile_link_save', { network: 'youtube', action: 'edit' });
    expect(JSON.stringify(analytics.trackEvent.calls.allArgs())).not.toContain('secret');
  });

  it('tracks a deleted link, but not a failed save', () => {
    spyOn(window, 'confirm').and.returnValue(true);
    component.socialLinks.set(SocialNetworkType.Discord, {
      socialNetworkType: SocialNetworkType.Discord, path: 'invite', fullUrl: '', isEditing: false,
    } as any);
    component.deleteLink(SocialNetworkType.Discord);
    expect(analytics.trackEvent).toHaveBeenCalledOnceWith('profile_link_delete', { network: 'discord' });

    analytics.trackEvent.calls.reset();
    settings.updateSocialLinks.and.returnValue(of({ success: false } as any));
    component.startAdding(SocialNetworkType.Steam);
    component.socialLinks.get(SocialNetworkType.Steam)!.path = 'id';
    component.saveLink(SocialNetworkType.Steam);
    expect(analytics.trackEvent).not.toHaveBeenCalled();
  });
});
