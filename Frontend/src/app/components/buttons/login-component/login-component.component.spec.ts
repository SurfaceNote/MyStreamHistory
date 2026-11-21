import { TestBed } from '@angular/core/testing';

import { LoginComponentComponent } from './login-component.component';
import { AuthService } from '../../../auth/auth.service';
import { AnalyticsService } from '../../../service/analytics.service';

describe('LoginComponentComponent', () => {
  it('reports where sign-in started before leaving for Twitch, and works again afterwards', async () => {
    const calls: string[] = [];
    const auth = { loginWithTwitch: jasmine.createSpy('loginWithTwitch').and.callFake(() => { calls.push('twitch'); }) };
    const analytics = jasmine.createSpyObj<AnalyticsService>('AnalyticsService', ['trackEventBeforeLeaving']);
    analytics.trackEventBeforeLeaving.and.callFake(async () => { calls.push('event'); });
    TestBed.configureTestingModule({
      imports: [LoginComponentComponent],
      providers: [{ provide: AuthService, useValue: auth }, { provide: AnalyticsService, useValue: analytics }],
    });
    const fixture = TestBed.createComponent(LoginComponentComponent);
    fixture.componentRef.setInput('placement', 'header');
    fixture.detectChanges();

    const first = fixture.componentInstance.loginWithTwitch();
    const doubleClick = fixture.componentInstance.loginWithTwitch();
    await Promise.all([first, doubleClick]);
    expect(calls).toEqual(['event', 'twitch']);
    expect(analytics.trackEventBeforeLeaving).toHaveBeenCalledOnceWith('login_start', { location: 'header' });

    // Returning with the back button must not leave a dead button.
    await fixture.componentInstance.loginWithTwitch();
    expect(auth.loginWithTwitch).toHaveBeenCalledTimes(2);
  });
});
