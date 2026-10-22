import { Component, inject, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { NavigationEnd, NavigationStart, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { ResponseStatusService } from '../core/api/response-status.service';
import { LocaleService } from './locale.service';
import { DISMISSED_LANGUAGE_SUGGESTIONS, LanguageSuggestionService } from './language-suggestion.service';

@Component({ standalone: true, template: '' })
class SuggestionHost { readonly suggestion = inject(LanguageSuggestionService); }

describe('Preferred language suggestions', () => {
  let events: Subject<NavigationStart | NavigationEnd>;
  let router: { url: string; navigated: boolean; events: typeof events; navigateByUrl: jasmine.Spy };
  let locale: LocaleService;
  let suggestion: LanguageSuggestionService;
  let failed: ReturnType<typeof signal<boolean>>;

  function navigate(url: string): void {
    router.url = url;
    events.next(new NavigationStart(1, url));
    router.navigated = true;
    events.next(new NavigationEnd(1, url, url));
  }
  function hydrate(url = '/es/profile/123?period=7d#games'): void {
    suggestion = TestBed.inject(LanguageSuggestionService);
    locale = TestBed.inject(LocaleService);
    navigate(url);
    expect(suggestion.offer()).toBeNull();
    TestBed.createComponent(SuggestionHost).detectChanges();
  }
  beforeEach(() => {
    localStorage.removeItem('preferred_language');
    document.cookie = 'preferred_language=; Path=/; Max-Age=0';
    sessionStorage.removeItem(DISMISSED_LANGUAGE_SUGGESTIONS);
    spyOnProperty(navigator, 'languages', 'get').and.returnValue(['fr-FR', 'de-DE', 'en-US']);
    events = new Subject();
    failed = signal(false);
    router = { url: '/', navigated: false, events, navigateByUrl: jasmine.createSpy().and.resolveTo(true) };
    TestBed.configureTestingModule({ imports: [SuggestionHost], providers: [
      { provide: Router, useValue: router }, { provide: ResponseStatusService, useValue: { failed } }
    ] });
  });
  afterEach(() => {
    localStorage.removeItem('preferred_language');
    document.cookie = 'preferred_language=; Path=/; Max-Age=0';
    sessionStorage.removeItem(DISMISSED_LANGUAGE_SUGGESTIONS);
    document.documentElement.lang = 'en';
  });

  it('waits for hydration and offers a saved language without redirecting the direct URL', () => {
    document.cookie = 'preferred_language=ru; Path=/';
    hydrate();
    expect(suggestion.offer()).toEqual({ language: 'ru', path: '/ru/profile/123?period=7d#games', article: false });
    expect(locale.language()).toBe('es');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
    expect(locale.t('languageSuggestion.open', {}, suggestion.offer()!.language)).toBe('Открыть на русском');
  });
  it('preserves objects and URL suffixes on streams, viewers, feeds and explicit language homes', () => {
    hydrate();
    for (const path of ['/stream/abc', '/profile/123/viewer/456', '/news', '/reviews', '/']) {
      navigate('/es' + path + '?period=7d#details');
      expect(suggestion.offer()?.path).withContext(path).toBe('/de' + path + '?period=7d#details');
    }
  });
  it('only offers published article translations, using browser preferences after an unavailable manual choice', () => {
    document.cookie = 'preferred_language=cs; Path=/';
    hydrate('/es/news/source?from=share#body');
    expect(suggestion.offer()).toBeNull();
    locale.alternatives.set([{ language: 'es', path: '/es/news/source' }, { language: 'de', path: '/de/news/ubersetzung' }]);
    expect(suggestion.offer()).toEqual({ language: 'de', path: '/de/news/ubersetzung?from=share#body', article: true });
  });
  it('does not suggest a lower priority translation when the current version is already preferred', () => {
    document.cookie = 'preferred_language=es; Path=/';
    hydrate('/es/reviews/source');
    locale.alternatives.set([{ language: 'es', path: '/es/reviews/source' }, { language: 'de', path: '/de/reviews/translation' }]);
    expect(suggestion.offer()).toBeNull();
  });
  it('leaves independent articles alone when they have no preferred translation', () => {
    hydrate('/es/news/independent');
    locale.alternatives.set([{ language: 'es', path: '/es/news/independent' }]);
    expect(suggestion.offer()).toBeNull();
  });
  it('suppresses a dismissed language pair across public pages and retains other pairs', () => {
    hydrate();
    suggestion.dismiss();
    expect(suggestion.offer()).toBeNull();
    expect(JSON.parse(sessionStorage.getItem(DISMISSED_LANGUAGE_SUGGESTIONS)!)).toEqual(['es:de']);
    navigate('/es/stream/abc');
    expect(suggestion.offer()).toBeNull();
    navigate('/ru/stream/abc');
    expect(suggestion.offer()?.language).toBe('de');
  });
  it('restores session dismissals and ignores malformed session storage', () => {
    sessionStorage.setItem(DISMISSED_LANGUAGE_SUGGESTIONS, JSON.stringify(['es:de', null, 'xx:de', 'ru:ru']));
    hydrate();
    expect(suggestion.offer()).toBeNull();
    navigate('/ru/profile/123');
    expect(suggestion.offer()?.language).toBe('de');
  });
  it('works when session storage cannot be read or written', () => {
    spyOn(sessionStorage, 'getItem').and.throwError('Storage unavailable');
    spyOn(sessionStorage, 'setItem').and.throwError('Storage unavailable');
    hydrate();
    expect(suggestion.offer()?.language).toBe('de');
    suggestion.dismiss();
    navigate('/es/stream/abc');
    expect(suggestion.offer()).toBeNull();
  });
  it('ignores invalid session JSON', () => {
    sessionStorage.setItem(DISMISSED_LANGUAGE_SUGGESTIONS, '{invalid');
    hydrate();
    expect(suggestion.offer()?.language).toBe('de');
  });
  it('excludes service pages, unknown pages, the negotiated root and failed requests', () => {
    hydrate('/de/profile/123');
    expect(suggestion.offer()).toBeNull();
    // Saved English also keeps '/' stable instead of triggering homepage negotiation.
    locale.rememberLanguage('en');
    for (const path of ['/', '/settings', '/admin/publications', '/analytics/community', '/overlay/top-viewers/123', '/es/unknown']) {
      navigate(path);
      expect(suggestion.offer()).withContext(path).toBeNull();
    }
    navigate('/es/profile/123');
    expect(suggestion.offer()?.language).toBe('en');
    failed.set(true);
    expect(suggestion.offer()).toBeNull();
  });
  it('hides the old article suggestion during navigation until the new translation data arrives', () => {
    hydrate('/es/news/first');
    locale.alternatives.set([{ language: 'de', path: '/de/news/first' }]);
    expect(suggestion.offer()?.path).toBe('/de/news/first');
    router.url = '/es/news/second';
    events.next(new NavigationStart(2, router.url));
    expect(suggestion.offer()).toBeNull();
    events.next(new NavigationEnd(2, router.url, router.url));
    expect(suggestion.offer()).toBeNull();
    locale.alternatives.set([{ language: 'de', path: '/de/news/second' }]);
    expect(suggestion.offer()?.path).toBe('/de/news/second');
  });
});
