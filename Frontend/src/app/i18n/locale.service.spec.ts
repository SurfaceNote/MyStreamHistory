import { TestBed } from '@angular/core/testing';
import { Router, NavigationStart } from '@angular/router';
import { Subject } from 'rxjs';
import { LocaleService } from './locale.service';
import { DICTIONARIES, en } from './translations';
import { LANGUAGES, localizedPath, languageFromUrl, safeReturnUrl } from './languages';

describe('Localization', () => {
  let locale: LocaleService;
  let events: Subject<NavigationStart>;
  let router: { url: string; events: Subject<NavigationStart> };
  function navigate(url: string) { router.url = url; events.next(new NavigationStart(1, url)); }
  beforeEach(() => {
    events = new Subject(); router = { url: '/', events };
    TestBed.configureTestingModule({ providers: [{ provide: Router, useValue: router }] });
    locale = TestBed.inject(LocaleService);
  });
  afterEach(() => {
    localStorage.removeItem('preferred_language');
    document.cookie = 'preferred_language=; Path=/; Max-Age=0';
    document.documentElement.lang = 'en';
  });
  it('remembers an explicit choice and does not overwrite it when a different-language link is opened', () => {
    locale.rememberLanguage('es');
    expect(localStorage.getItem('preferred_language')).toBe('es');
    expect(document.cookie).toContain('preferred_language=es');
    navigate('/de/news/example');
    expect(locale.language()).toBe('de');
    expect(locale.preferredLanguage()).toBe('es');
    navigate('/settings');
    expect(locale.path('/profile/123')).toBe('/es/profile/123');
  });
  it('gives a saved English choice precedence over a non-English browser preference', () => {
    spyOnProperty(navigator, 'languages', 'get').and.returnValue(['ru-RU', 'cs']);
    expect(locale.preferredLanguage()).toBe('ru');
    locale.rememberLanguage('en');
    expect(locale.preferredLanguage()).toBe('en');
  });
  it('uses browser preferences in order and falls back to English if none is supported', () => {
    const languages = spyOnProperty(navigator, 'languages', 'get').and.returnValue(['fr-FR', 'cs-CZ', 'de']);
    expect(locale.preferredLanguage()).toBe('cs');
    languages.and.returnValue(['fr-FR', 'ja']);
    expect(locale.preferredLanguage()).toBe('en');
  });
  it('contains a complete nonempty dictionary with identical placeholders for all five languages', () => {
    const placeholders = (text: string) => (text.match(/\{\w+\}/g) || []).sort();
    for (const { code } of LANGUAGES) {
      const dictionary = DICTIONARIES[code];
      expect(Object.keys(dictionary).sort()).toEqual(Object.keys(en).sort());
      for (const key of Object.keys(en) as (keyof typeof en)[]) {
        expect(dictionary[key].trim()).withContext(code + ':' + key).not.toBe('');
        expect(placeholders(dictionary[key])).withContext(code + ':' + key).toEqual(placeholders(en[key]));
      }
    }
  });
  it('preserves the public object, query and fragment across languages', () => {
    for (const { code } of LANGUAGES) {
      expect(localizedPath('/ru/profile/123?period=7d#games', code)).toBe((code === 'en' ? '' : '/' + code) + '/profile/123?period=7d#games');
      expect(languageFromUrl(localizedPath('/', code))).toBe(code);
    }
    expect(localizedPath('/ru?x=1', 'en')).toBe('/?x=1');
  });
  it('builds language links from the navigation URL before Router.url is committed', () => {
    events.next(new NavigationStart(1, '/es/profile/123?period=7d#games'));
    expect(router.url).toBe('/');
    expect(locale.switchPath('ru')).toBe('/ru/profile/123?period=7d#games');
    events.next(new NavigationStart(2, '/es/news/original?from=share#body'));
    locale.alternatives.set([{ language: 'de', path: '/de/news/translation' }]);
    expect(locale.switchPath('de')).toBe('/de/news/translation?from=share#body');
    expect(locale.switchPath('cs')).toBe('/cs/news');
  });
  it('keeps service URLs stable while public links retain the chosen language', () => {
    navigate('/cs/profile/123'); expect(locale.language()).toBe('cs');
    navigate('/settings');
    expect(locale.path('/profile/123')).toBe('/cs/profile/123');
    expect(locale.switchPath('ru')).toBe('/settings');
    locale.rememberLanguage('ru');
    expect(locale.language()).toBe('ru');
    expect(locale.path('/profile/123')).toBe('/ru/profile/123');
    expect(locale.path('/admin/publications')).toBe('/admin/publications');
  });
  it('uses only actual translations and makes missing-language links point to the feed', () => {
    navigate('/ru/news/novost?source=test#body');
    locale.alternatives.set([{ language: 'ru', path: '/ru/news/novost' }, { language: 'de', path: '/de/news/nachricht' }]);
    expect(locale.switchPath('de')).toBe('/de/news/nachricht?source=test#body'); expect(locale.hasTranslation('de')).toBeTrue();
    expect(locale.switchPath('es')).toBe('/es/news'); expect(locale.hasTranslation('es')).toBeFalse();
    navigate('/ru/profile/123'); expect(locale.alternatives()).toBeNull();
    expect(locale.switchPath('es')).toBe('/es/profile/123');
  });
  it('formats Russian plurals and Czech, German and Spanish numbers and durations', () => {
    navigate('/ru/');
    expect(locale.plural('streamerCount', 1)).toBe('1 стример');
    expect(locale.plural('streamerCount', 2)).toBe('2 стримера');
    expect(locale.plural('streamerCount', 5)).toBe('5 стримеров');
    expect(locale.duration(125)).toBe('2ч 5мин');
    for (const language of ['cs', 'de', 'es']) { navigate('/' + language + '/'); expect(locale.number(1.5)).toBe('1,5'); }
  });
  it('accepts internal OAuth return URLs and rejects external or disguised URLs', () => {
    expect(safeReturnUrl('/de/stream/abc?x=1#chart')).toBe('/de/stream/abc?x=1#chart');
    for (const url of ['https://evil.test', '//evil.test', '/%2fevil.test', '/news/../../callback', '/news/\\evil', '/admin', '/ru/news/%0aevil']) {
      expect(safeReturnUrl(url)).withContext(url).toBeNull();
    }
  });
});
