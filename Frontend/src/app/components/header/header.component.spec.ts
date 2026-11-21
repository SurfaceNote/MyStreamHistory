import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Subject, of } from 'rxjs';
import { HeaderComponent } from './header.component';
import { AuthService } from '../../auth/auth.service';
import { AnalyticsService } from '../../service/analytics.service';
import { LocaleService } from '../../i18n/locale.service';

describe('Header authentication', () => {
  for (const token of ['stored-session', null]) {
    it(`shows only a placeholder until auth resolves to ${token ? 'profile' : 'login'}`, () => {
      const tokens = new Subject<string | null>();
      TestBed.configureTestingModule({
        imports: [HeaderComponent],
        providers: [
          provideRouter([]),
          {provide: AuthService, useValue: {
            getAccessTokenObservable: () => tokens.asObservable(),
            getUsernameObservable: () => of('kination'),
            getUsernameFromToken: () => 'kination',
            getTwitchIdFromToken: () => '106010980',
            isAdmin: () => false
          }}
        ]
      });
      const fixture = TestBed.createComponent(HeaderComponent);
      fixture.detectChanges();
      const element: HTMLElement = fixture.nativeElement;
      expect(element.querySelector('.auth-placeholder')).not.toBeNull();
      expect(getComputedStyle(element.querySelector('.auth-placeholder')!).visibility).toBe('hidden');
      expect(element.querySelector('app-login-component')).toBeNull();
      expect(element.querySelector('.user-menu')).toBeNull();
      tokens.next(token);
      fixture.detectChanges();
      expect(element.querySelector('.auth-placeholder')).toBeNull();
      expect(!!element.querySelector('app-login-component')).toBe(!token);
      expect(!!element.querySelector('.user-menu')).toBe(!!token);
      expect(!!element.querySelector('nav a[href="/analytics/community"]')).toBe(!!token);
      if (token) {
        expect(element.querySelector('.profile-link')?.textContent).toBe('kination');
        expect(element.querySelector('button[aria-label="Settings"]')).not.toBeNull();
      }
      fixture.destroy();
    });
  }

  it('reports a language switch with the previous language, but not re-selecting the current one', () => {
    const analytics = jasmine.createSpyObj<AnalyticsService>('AnalyticsService', ['trackEvent']);
    const locale = jasmine.createSpyObj<LocaleService>('LocaleService', ['rememberLanguage', 'language']);
    locale.language.and.returnValue('en');
    TestBed.configureTestingModule({
      imports: [HeaderComponent],
      providers: [
        { provide: AnalyticsService, useValue: analytics }, { provide: LocaleService, useValue: locale },
        { provide: AuthService, useValue: { getAccessTokenObservable: () => of(null) } }
      ]
    });
    TestBed.overrideComponent(HeaderComponent, { set: { template: '' } });
    const header = TestBed.createComponent(HeaderComponent).componentInstance;
    header.selectLanguage('en');
    header.selectLanguage('ru');
    expect(analytics.trackEvent).toHaveBeenCalledOnceWith('language_select', { language: 'ru', from: 'en' });
    expect(locale.rememberLanguage.calls.allArgs()).toEqual([['en'], ['ru']]);
  });
});
