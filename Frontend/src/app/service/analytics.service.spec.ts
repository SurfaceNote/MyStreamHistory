import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { AnalyticsService } from './analytics.service';

type TestWindow = Window & { gtag?: jasmine.Spy; mshStartAnalytics?: jasmine.Spy };

describe('AnalyticsService', () => {
  const browser = window as TestWindow;
  let service: AnalyticsService;
  let originalGtag: TestWindow['gtag'];

  beforeEach(() => {
    originalGtag = browser.gtag;
    service = TestBed.inject(AnalyticsService);
  });
  afterEach(() => {
    if (originalGtag === undefined) delete browser.gtag; else browser.gtag = originalGtag;
    delete browser.mshStartAnalytics;
  });

  it('starts the deferred tag when index.html provides it, and is safe without it', () => {
    expect(() => service.start()).not.toThrow();
    browser.mshStartAnalytics = jasmine.createSpy('mshStartAnalytics');
    service.start();
    expect(browser.mshStartAnalytics).toHaveBeenCalledTimes(1);
  });

  it('resolves a leaving event as soon as the tag confirms it', async () => {
    browser.gtag = jasmine.createSpy('gtag').and.callFake((_c: string, _n: string, p: any) => p.event_callback());
    await service.trackEventBeforeLeaving('login_start', { location: 'home' });
    expect(browser.gtag).toHaveBeenCalledWith('event', 'login_start', jasmine.objectContaining({ location: 'home', event_timeout: 600 }));
  });

  it('does not wait longer than the timeout when the tag never confirms', fakeAsync(() => {
    browser.gtag = jasmine.createSpy('gtag');
    let resolved = false;
    void service.trackEventBeforeLeaving('login_start').then(() => { resolved = true; });
    tick(599);
    expect(resolved).toBeFalse();
    tick(1);
    expect(resolved).toBeTrue();
  }));

  it('resolves immediately without the tag', async () => {
    delete browser.gtag;
    await expectAsync(service.trackEventBeforeLeaving('login_start')).toBeResolved();
  });
});
