import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';

import { CallbackComponent } from './callback.component';
import { AuthService } from '../../auth/auth.service';
import { AnalyticsService } from '../../service/analytics.service';
import { LocaleService } from '../../i18n/locale.service';

describe('CallbackComponent', () => {
  let calls: string[];
  let analytics: jasmine.SpyObj<AnalyticsService>;
  let auth: jasmine.SpyObj<AuthService>;
  let navigate: jasmine.Spy;

  async function signIn(params: Record<string, string>): Promise<void> {
    TestBed.configureTestingModule({
      imports: [CallbackComponent],
      providers: [
        { provide: AuthService, useValue: auth },
        { provide: AnalyticsService, useValue: analytics },
        { provide: ActivatedRoute, useValue: { queryParams: of(params) } },
        { provide: Router, useValue: { navigateByUrl: navigate } },
        {
          provide: LocaleService,
          useValue: { t: (key: string) => key, path: (path: string | string[]) => Array.isArray(path) ? path.join('/') : path },
        },
      ],
    });
    const fixture = TestBed.createComponent(CallbackComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    await new Promise(resolve => setTimeout(resolve));
  }

  beforeEach(() => {
    calls = [];
    auth = jasmine.createSpyObj<AuthService>('AuthService',
      ['consumeReturnUrl', 'handleTwitchCallback', 'getTwitchIdFromToken', 'isNewChannel']);
    auth.consumeReturnUrl.and.returnValue('/news');
    auth.getTwitchIdFromToken.and.returnValue('123');
    auth.isNewChannel.and.returnValue(false);
    analytics = jasmine.createSpyObj<AnalyticsService>('AnalyticsService', ['start', 'trackEvent']);
    analytics.start.and.callFake(() => { calls.push('start'); });
    analytics.trackEvent.and.callFake((name: string) => { calls.push(name); });
    navigate = jasmine.createSpy('navigateByUrl').and.callFake((url: string) => {
      calls.push('navigate ' + url);
      return Promise.resolve(true);
    });
  });

  it('starts analytics after leaving the callback URL and records a returning sign-in', async () => {
    auth.handleTwitchCallback.and.returnValue(of({ isNewUser: false }));
    await signIn({ code: 'one-time', state: 'state' });

    expect(calls).toEqual(['navigate /news', 'start', 'login']);
    expect(analytics.trackEvent).toHaveBeenCalledWith('login', { method: 'Twitch' });
  });

  it('records the first sign-in of a channel as sign_up', async () => {
    auth.handleTwitchCallback.and.returnValue(of({ isNewUser: true }));
    auth.isNewChannel.and.returnValue(true);
    await signIn({ code: 'one-time', state: 'state' });

    expect(navigate).toHaveBeenCalledWith('/profile/123');
    expect(analytics.trackEvent).toHaveBeenCalledOnceWith('sign_up', { method: 'Twitch' });
  });

  it('starts analytics without a sign-in event when sign-in failed', async () => {
    auth.handleTwitchCallback.and.returnValue(of(null));
    await signIn({ code: 'one-time', state: 'state' });
    expect(calls).toEqual(['navigate /news', 'start']);

    TestBed.resetTestingModule();
    calls = [];
    await signIn({});
    expect(calls).toEqual(['navigate /news', 'start']);
  });
});
