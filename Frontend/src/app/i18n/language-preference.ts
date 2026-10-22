import { Language, isLanguage } from './languages';

export const LANGUAGE_PREFERENCE_KEY = 'preferred_language';

function supportedLanguage(tag: string): Language | undefined {
  const language = tag.trim().toLowerCase().split(/[-_]/)[0];
  return isLanguage(language) ? language : undefined;
}

export function browserLanguage(tags: readonly string[]): Language {
  return browserLanguages(tags)[0] || 'en';
}

export function browserLanguages(tags: readonly string[]): Language[] {
  const languages: Language[] = [];
  for (const tag of tags) {
    const language = supportedLanguage(tag);
    if (language && !languages.includes(language)) languages.push(language);
  }
  return languages;
}

export function acceptLanguage(header: string | undefined): Language {
  const preferences = (header || '').split(',').map((entry, index) => {
    const [tag, ...parameters] = entry.trim().split(';');
    const qualityParameter = parameters.find(parameter => /^\s*q\s*=/i.test(parameter));
    const quality = qualityParameter === undefined ? 1 : Number(qualityParameter.split('=')[1].trim());
    return { tag, index, quality };
  }).filter(item => Number.isFinite(item.quality) && item.quality > 0 && item.quality <= 1)
    .sort((a, b) => b.quality - a.quality || a.index - b.index);
  return browserLanguage(preferences.map(item => item.tag));
}

export function languageCookie(header: string | undefined): Language | undefined {
  for (const entry of (header || '').split(';')) {
    const separator = entry.indexOf('=');
    if (entry.slice(0, separator).trim() !== LANGUAGE_PREFERENCE_KEY) continue;
    try {
      const value = decodeURIComponent(entry.slice(separator + 1).trim());
      if (isLanguage(value)) return value;
    } catch { /* Ignore malformed cookies. */ }
  }
  return undefined;
}
