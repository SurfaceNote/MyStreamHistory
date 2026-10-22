import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { HeaderComponent } from '../components/header/header.component';
import { SettingsComponent } from '../pages/settings/settings.component';
import { SettingsService } from '../service/settings.service';
import { StreamerService } from '../service/streamer.service';
import { TopViewersOverlayComponent } from '../pages/top-viewers-overlay/top-viewers-overlay.component';
import { LANGUAGES } from './languages';
import { LocaleService } from './locale.service';

describe('Localized service pages', () => {
  beforeEach(() => {
    localStorage.removeItem('preferred_language');
    document.cookie = 'preferred_language=; Path=/; Max-Age=0';
    spyOnProperty(navigator, 'languages', 'get').and.returnValue(['ru-RU', 'de-DE']);
    TestBed.configureTestingModule({ providers: [
      provideRouter([{ path: 'settings', component: SettingsComponent }]),
      { provide: AuthService, useValue: {
        getAccessTokenObservable: () => of('test'), getUsernameObservable: () => of('User'),
        getUsernameFromToken: () => 'User', getTwitchIdFromToken: () => '123', isAdmin: () => false
      } },
      { provide: SettingsService, useValue: {
        getSocialLinks: () => of({ socialLinks: [] }),
        getPlaythroughs: () => of({ playthroughs: [], availableGames: [] })
      } }
    ] });
  });
  afterEach(() => {
    localStorage.removeItem('preferred_language');
    document.cookie = 'preferred_language=; Path=/; Max-Age=0';
    document.documentElement.lang = 'en';
  });
  it('uses browser preferences, then switches all settings sections in place and preserves the URL', async () => {
    const harness = await RouterTestingHarness.create('/settings?source=test#profile');
    const header = TestBed.createComponent(HeaderComponent); header.detectChanges();
    const locale = TestBed.inject(LocaleService);
    const router = TestBed.inject(Router);
    expect(locale.language()).toBe('ru');
    const component = harness.routeDebugElement!.componentInstance as SettingsComponent;
    for (const { code } of LANGUAGES) {
      header.nativeElement.querySelector(`.language-menu a[lang="${code}"]`).click();
      header.detectChanges(); harness.detectChanges();
      expect(router.url).toBe('/settings?source=test#profile');
      expect(locale.language()).toBe(code);
      expect(document.documentElement.lang).toBe(code);
      component.onSectionChange('profile'); harness.detectChanges();
      expect(harness.routeNativeElement?.querySelector('.section-title')?.textContent).toBe(locale.t('ui.profile'));
      expect(harness.routeNativeElement?.querySelector('.section-description')?.textContent).toBe(locale.t('settings.socialDescription'));
      component.onSectionChange('playthroughs'); harness.detectChanges();
      expect(harness.routeNativeElement?.textContent).toContain(locale.t('settings.newPlaythrough'));
      expect(harness.routeNativeElement?.querySelector('option[value="Playing"]')?.textContent).toBe(locale.t('ui.playing'));
      component.onSectionChange('overlay'); harness.detectChanges();
      expect(harness.routeNativeElement?.textContent).toContain(locale.t('settings.overlayTitle'));
      expect(harness.routeNativeElement?.querySelector<HTMLInputElement>('#overlay-url')?.value).toContain('/overlay/top-viewers/123?language=' + code);
      expect(locale.path('/profile/123')).toBe(code === 'en' ? '/profile/123' : '/' + code + '/profile/123');
      expect(locale.switchPath(code)).toBe('/settings?source=test#profile');
    }
    header.destroy();
  });
  it('restores an explicitly saved English choice instead of the browser language', async () => {
    document.cookie = 'preferred_language=en; Path=/';
    const harness = await RouterTestingHarness.create('/settings');
    harness.detectChanges();
    expect(TestBed.inject(LocaleService).language()).toBe('en');
    expect(harness.routeNativeElement?.querySelector('.sidebar-title')?.textContent).toBe('Settings');
  });
  it('can render and destroy the OBS shell on the server without polling or accessing a global document', () => {
    const fetch = jasmine.createSpy('getTopViewers');
    TestBed.overrideProvider(PLATFORM_ID, { useValue: 'server' });
    TestBed.overrideProvider(StreamerService, { useValue: { getTopViewers: fetch } });
    const fixture = TestBed.createComponent(TopViewersOverlayComponent);
    fixture.detectChanges();
    fixture.destroy();
    expect(fetch).not.toHaveBeenCalled();
    expect(document.documentElement.classList.contains('obs-overlay')).toBeFalse();
  });
});
