import { TestBed } from '@angular/core/testing';
import { Router, NavigationStart } from '@angular/router';
import { Subject } from 'rxjs';
import { SeoService } from './seo.service';
import { LocaleService } from '../i18n/locale.service';

describe('Localized SEO', () => {
  let router: { url: string; events: Subject<NavigationStart> };
  let seo: SeoService;
  beforeEach(() => {
    router = { url: '/ru/news/test', events: new Subject() };
    TestBed.configureTestingModule({ providers: [{ provide: Router, useValue: router }] });
    seo = TestBed.inject(SeoService);
    router.events.next(new NavigationStart(1, router.url));
  });
  afterEach(() => { seo.clearAlternates(); document.documentElement.lang = 'en'; localStorage.removeItem('preferred_language'); });
  it('keeps self canonical and links only supplied published article equivalents', () => {
    seo.update({ title: 'Тест', description: 'Описание', alternatives: [
      { language: 'ru', path: '/ru/news/test' }, { language: 'en', path: '/news/original' }
    ] });
    expect(document.querySelector('link[rel=canonical]')!.getAttribute('href')).toBe('https://mystreamhistory.com/ru/news/test');
    expect(document.querySelectorAll('link[hreflang]').length).toBe(3);
    expect(document.querySelector('link[hreflang=de]')).toBeNull();
    expect(document.documentElement.lang).toBe('ru');
  });
  it('clears article alternatives and emits all five languages for shared stats', () => {
    seo.update({ title: 'Article', description: '', alternatives: [{ language: 'de', path: '/de/news/a' }] });
    router.url = '/cs/profile/123?x=1'; router.events.next(new NavigationStart(2, router.url));
    seo.update({ title: 'Statistiky', description: '' });
    expect(document.querySelector('link[hreflang=de]')!.getAttribute('href')).toBe('https://mystreamhistory.com/de/profile/123');
    expect(document.querySelectorAll('link[hreflang]').length).toBe(6);
    expect(document.querySelector('meta[property="og:locale"]')!.getAttribute('content')).toBe('cs_CZ');
    seo.update({ title: 'Private', description: '', noIndex: true });
    expect(document.querySelectorAll('link[hreflang]').length).toBe(0);
  });
  it('does not invent alternatives for an independent article', () => {
    seo.update({ title: 'Independent', description: '' });
    expect(document.querySelectorAll('link[hreflang]').length).toBe(0);
  });
});
