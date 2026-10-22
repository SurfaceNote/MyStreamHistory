import { acceptLanguage, browserLanguage, languageCookie } from './language-preference';

describe('Language negotiation', () => {
  it('handles regional language tags and browser preference order', () => {
    expect(browserLanguage(['fr-FR', 'es-MX', 'ru'])).toBe('es');
    expect(browserLanguage(['cs-CZ'])).toBe('cs');
    expect(browserLanguage(['DE-ch'])).toBe('de');
    expect(browserLanguage([])).toBe('en');
  });
  it('honors HTTP quality weights and excludes disabled or invalid preferences', () => {
    expect(acceptLanguage('en;q=0.4,ru-RU;q=0.9,de;q=0.8')).toBe('ru');
    expect(acceptLanguage('ru;q=0,cs-CZ;q=0.5')).toBe('cs');
    expect(acceptLanguage('ru;q=invalid,es;q=2,de;q=0.7')).toBe('de');
    expect(acceptLanguage('es;q=0.8,cs;q=0.8')).toBe('es');
    expect(acceptLanguage(undefined)).toBe('en');
    expect(acceptLanguage('*')).toBe('en');
  });
  it('accepts only supported cookie values and ignores other cookies', () => {
    expect(languageCookie('session=abc; preferred_language=en; other=ru')).toBe('en');
    expect(languageCookie('preferred_language=cs')).toBe('cs');
    expect(languageCookie('preferred_language=%ZZ')).toBeUndefined();
    expect(languageCookie('preferred_language=xx')).toBeUndefined();
    expect(languageCookie('preferred_language_backup=ru')).toBeUndefined();
    expect(languageCookie(undefined)).toBeUndefined();
  });
});
