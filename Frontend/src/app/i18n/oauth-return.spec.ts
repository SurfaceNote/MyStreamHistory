import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { AuthService } from '../auth/auth.service';
describe('OAuth language return', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideHttpClient(),
    { provide: Router, useValue: { url: '/callback', events: new Subject() } }
  ] }));
  afterEach(() => { sessionStorage.removeItem('twitchReturnUrl'); localStorage.removeItem('preferred_language'); });
  it('restores and consumes each saved public language URL exactly once', () => {
    const auth = TestBed.inject(AuthService);
    for (const prefix of ['', '/ru', '/cs', '/de', '/es']) {
      const url = prefix + '/profile/123?period=7d#games';
      sessionStorage.setItem('twitchReturnUrl', url);
      expect(auth.consumeReturnUrl()).toBe(url);
      expect(sessionStorage.getItem('twitchReturnUrl')).toBeNull();
    }
  });
  it('rejects external return URLs and falls back to the last public language', () => {
    localStorage.setItem('preferred_language', 'es');
    sessionStorage.setItem('twitchReturnUrl', '//evil.test');
    expect(TestBed.inject(AuthService).consumeReturnUrl()).toBe('/es/');
  });
});
