export const LANGUAGES = [
  { code: 'en', name: 'English', locale: 'en-US', og: 'en_US', flag: '/images/flags/gb.svg' },
  { code: 'ru', name: 'Русский', locale: 'ru-RU', og: 'ru_RU', flag: '/images/flags/ru.svg' },
  { code: 'cs', name: 'Čeština', locale: 'cs-CZ', og: 'cs_CZ', flag: '/images/flags/cz.svg' },
  { code: 'de', name: 'Deutsch', locale: 'de-DE', og: 'de_DE', flag: '/images/flags/de.svg' },
  { code: 'es', name: 'Español', locale: 'es-ES', og: 'es_ES', flag: '/images/flags/es.svg' },
] as const;
export type Language = typeof LANGUAGES[number]['code'];
export function isLanguage(value: unknown): value is Language {
  return LANGUAGES.some(language => language.code === value);
}
export function languageFromUrl(url: string): Language {
  const first = url.split(/[?#]/)[0].split('/')[1];
  return isLanguage(first) ? first : 'en';
}
export function unlocalizedPath(url: string): string {
  const path = url.replace(/^\/(ru|cs|de|es)(?=\/|[?#]|$)/, '');
  return path.startsWith('/') ? path : '/' + path;
}
export function localizedPath(url: string, language: Language): string {
  const path = unlocalizedPath(url);
  return language === 'en' ? path : '/' + language + (path.startsWith('/') ? path : '/' + path);
}
export function isPublicPath(url: string): boolean {
  return /^\/(?:$|[?#]|news(?:[/?#]|$)|reviews(?:[/?#]|$)|profile\/|stream\/)/.test(unlocalizedPath(url));
}
export function isServicePath(url: string): boolean {
  return /^\/(?:settings|analytics\/community|admin(?:\/publications(?:\/[^/]+(?:\/preview)?)?)?|callback|overlay\/top-viewers\/[^/]+)\/?$/.test(url.split(/[?#]/)[0]);
}
export function safeReturnUrl(value: string | null): string | null {
  if (!value || !value.startsWith('/') || value.startsWith('//') || /[\\\r\n]/.test(value)) return null;
  try {
    const decoded = decodeURIComponent(value);
    if (decoded.startsWith('//') || /[\\\r\n]/.test(decoded)) return null;
    const url = new URL(value, 'https://mystreamhistory.invalid');
    return url.origin === 'https://mystreamhistory.invalid' && isPublicPath(url.pathname) ? url.pathname + url.search + url.hash : null;
  } catch { return null; }
}
