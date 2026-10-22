import { Component, inject } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, NavigationStart, NavigationEnd } from '@angular/router';
import { Subject } from 'rxjs';
import { LocaleService } from './locale.service';

@Component({ standalone: true, template: '' })
class PreferenceHost { readonly locale = inject(LocaleService); }

describe('Homepage preference routing', () => {
  let events: Subject<NavigationStart | NavigationEnd>;
  let router: { url: string; navigated: boolean; events: typeof events; navigateByUrl: jasmine.Spy };
  beforeEach(() => {
    events = new Subject();
    router = { url: '/', navigated: false, events, navigateByUrl: jasmine.createSpy().and.resolveTo(true) };
    localStorage.removeItem('preferred_language');
    document.cookie = 'preferred_language=; Path=/; Max-Age=0';
    TestBed.configureTestingModule({ imports: [PreferenceHost], providers: [{ provide: Router, useValue: router }] });
  });
  afterEach(() => {
    localStorage.removeItem('preferred_language');
    document.cookie = 'preferred_language=; Path=/; Max-Age=0';
    document.documentElement.lang = 'en';
  });
  it('waits for initial navigation and respects an explicit language URL', () => {
    localStorage.setItem('preferred_language', 'de');
    const fixture = TestBed.createComponent(PreferenceHost);
    events.next(new NavigationStart(1, '/ru/'));
    fixture.detectChanges();
    expect(router.navigateByUrl).not.toHaveBeenCalled();
    router.url = '/ru/'; router.navigated = true;
    events.next(new NavigationEnd(1, '/ru/', '/ru/'));
    expect(router.navigateByUrl).not.toHaveBeenCalled();
    expect(fixture.componentInstance.locale.language()).toBe('ru');
  });
  it('uses browser preferences for a completed bare homepage navigation without adding history', () => {
    spyOnProperty(navigator, 'languages', 'get').and.returnValue(['fr-FR', 'cs-CZ']);
    router.navigated = true;
    TestBed.createComponent(PreferenceHost).detectChanges();
    expect(router.navigateByUrl).toHaveBeenCalledOnceWith('/cs/', { replaceUrl: true });
  });
  it('keeps a saved English choice on the homepage despite a Russian browser preference', () => {
    spyOnProperty(navigator, 'languages', 'get').and.returnValue(['ru-RU']);
    router.navigated = true;
    document.cookie = 'preferred_language=en; Path=/';
    TestBed.createComponent(PreferenceHost).detectChanges();
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });
  it('uses the explicit OBS link language without replacing the saved website preference', () => {
    document.cookie = 'preferred_language=en; Path=/';
    const locale = TestBed.inject(LocaleService);
    router.url = '/overlay/top-viewers/123?language=cs'; router.navigated = true;
    events.next(new NavigationStart(1, router.url));
    // The first render remains independent of browser storage.
    expect(locale.language()).toBe('en');
    const fixture = TestBed.createComponent(PreferenceHost);
    fixture.detectChanges();
    expect(fixture.componentInstance.locale.language()).toBe('cs');
    expect(document.cookie).toContain('preferred_language=en');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });
});
