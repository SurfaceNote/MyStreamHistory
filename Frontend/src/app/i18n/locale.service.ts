import { DOCUMENT, isPlatformBrowser, registerLocaleData } from '@angular/common';
import ru from '@angular/common/locales/ru';
import cs from '@angular/common/locales/cs';
import de from '@angular/common/locales/de';
import es from '@angular/common/locales/es';
import { Injectable, PLATFORM_ID, inject, signal, computed, afterNextRender } from '@angular/core';
import { NavigationStart, NavigationEnd, Router, UrlTree } from '@angular/router';
import { LANGUAGES, Language, isLanguage, languageFromUrl, localizedPath, isPublicPath, isServicePath, unlocalizedPath } from './languages';
import { DICTIONARIES, TranslationKey } from './translations';
import { LANGUAGE_PREFERENCE_KEY, browserLanguages, languageCookie } from './language-preference';
[ru, cs, de, es].forEach(data => registerLocaleData(data));
export interface LanguageAlternative { language: Language; path: string; }
export type LocalizedMessage = TranslationKey | '' | {
  key: TranslationKey;
  params?: Record<string, string | number> | (() => Record<string, string | number>);
};
@Injectable({ providedIn: 'root' })
export class LocaleService {
  private readonly document = inject(DOCUMENT);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly router = inject(Router);
  readonly language = signal<Language>('en');
  readonly locale = computed(() => LANGUAGES.find(item => item.code === this.language())!.locale);
  readonly alternatives = signal<LanguageAlternative[] | null>(null);
  readonly languages = LANGUAGES;
  private readonly currentUrl = signal('/');
  private preferred: Language = 'en';
  private lastPublicLanguage?: Language;
  private manualPreference: Language | undefined;
  private hydrated = false;
  private readonly preferenceRevision = signal(0);
  constructor() {
    const location = this.document.location;
    this.setUrl((location?.pathname || '/') + (location?.search || '') + (location?.hash || ''));
    afterNextRender(() => {
      this.hydrated = true;
      // Preserve preferences stored by earlier versions, without replacing a cookie.
      const saved = this.savedLanguage();
      let cookie: Language | undefined;
      try { cookie = languageCookie(this.document.cookie); } catch { /* Cookies are optional. */ }
      if (saved && !cookie) this.rememberLanguage(saved);
      if (isServicePath(this.currentUrl())) this.setUrl(this.currentUrl());
      // Before initial navigation finishes Router.url is still '/', even for /ru/.
      if (this.router.navigated) this.openPreferredHome(this.router.url);
    });
    this.router.events.subscribe(event => {
      if (event instanceof NavigationStart) this.setUrl(event.url);
      if (event instanceof NavigationEnd) {
        if (this.currentUrl() !== event.urlAfterRedirects) this.setUrl(event.urlAfterRedirects);
        if (this.hydrated) this.openPreferredHome(event.urlAfterRedirects);
      }
    });
  }
  private setUrl(url: string): void {
    this.currentUrl.set(url);
    const overlayLanguage = /^\/overlay\//.test(url) ? new URL(url, 'https://local.invalid').searchParams.get('language') : null;
    this.language.set(isServicePath(url) && this.browser && this.hydrated
      ? isLanguage(overlayLanguage) ? overlayLanguage : this.savedLanguage() || this.lastPublicLanguage || this.preferredLanguage()
      : languageFromUrl(url));
    this.document.documentElement.lang = this.language();
    this.alternatives.set(null);
    if (this.browser && isPublicPath(url)) {
      this.preferred = this.language();
      this.lastPublicLanguage = this.language();
    }
  }
  private savedLanguage(): Language | undefined {
    if (!this.browser) return undefined;
    try {
      const cookie = languageCookie(this.document.cookie);
      if (cookie) return cookie;
    } catch { /* Cookies are optional. */ }
    try {
      const saved = localStorage.getItem(LANGUAGE_PREFERENCE_KEY);
      if (isLanguage(saved)) return saved;
    } catch { /* Storage is optional. */ }
    return this.manualPreference;
  }
  preferredLanguage(): Language {
    return this.preferenceLanguages()[0];
  }
  preferenceLanguages(): Language[] {
    this.preferenceRevision();
    const navigator = this.document.defaultView?.navigator;
    const saved = this.savedLanguage();
    return [...new Set<Language>([
      ...(saved ? [saved] : []),
      ...browserLanguages(navigator?.languages?.length
        ? navigator.languages : navigator?.language ? [navigator.language] : []),
      'en'
    ])];
  }
  rememberLanguage(language: Language): void {
    if (!this.browser || !isLanguage(language)) return;
    this.manualPreference = language;
    this.preferred = language;
    try { localStorage.setItem(LANGUAGE_PREFERENCE_KEY, language); } catch { /* Storage is optional. */ }
    try {
      const secure = this.document.location?.protocol === 'https:' ? '; Secure' : '';
      this.document.cookie = `${LANGUAGE_PREFERENCE_KEY}=${language}; Path=/; Max-Age=31536000; SameSite=Lax${secure}`;
    } catch { /* Cookies are optional. */ }
    this.preferenceRevision.update(value => value + 1);
    if (isServicePath(this.currentUrl())) {
      this.language.set(language);
      this.document.documentElement.lang = language;
    }
  }
  private openPreferredHome(url: string): void {
    if (!this.browser || url.split(/[?#]/)[0] !== '/') return;
    const language = this.preferredLanguage();
    if (language !== 'en') void this.router.navigateByUrl(localizedPath(url, language), { replaceUrl: true });
  }
  publicLanguage(): Language {
    if (isPublicPath(this.currentUrl()) || (isServicePath(this.currentUrl()) && this.hydrated)) return this.language();
    return this.savedLanguage() || this.preferred;
  }
  path(value: string | readonly unknown[], language = this.publicLanguage()): string {
    const url = Array.isArray(value) ? value.map(String).join('/').replace(/\/+/g, '/') : value as string;
    return isPublicPath(url) ? localizedPath(url, language) : url;
  }
  switchPath(language: Language): string {
    const url = this.currentUrl();
    const article = /^\/(news|reviews)\/[^/?#]+/.exec(unlocalizedPath(url));
    if (article) {
      const version = this.alternatives()?.find(item => item.language === language);
      return version ? version.path + (url.match(/[?#].*$/)?.[0] || '') : localizedPath('/' + article[1], language);
    }
    return isPublicPath(url) ? localizedPath(url, language) : isServicePath(url) ? url : localizedPath('/', language);
  }
  switchUrl(language: Language): UrlTree {
    // RouterLink treats a string as a path segment; parse the query and fragment explicitly.
    return this.router.parseUrl(this.switchPath(language));
  }
  hasTranslation(language: Language): boolean {
    if (!/^\/(news|reviews)\/[^/?#]+/.test(unlocalizedPath(this.currentUrl()))) return true;
    return !!this.alternatives()?.some(item => item.language === language);
  }
  t(key: TranslationKey, params: Record<string, string | number> = {}, language = this.language()): string {
    const value = DICTIONARIES[language][key];
    return value.replace(/\{(\w+)\}/g, (_, name: string) => String(params[name] ?? '{' + name + '}'));
  }
  message(message: LocalizedMessage): string {
    if (!message) return '';
    return typeof message === 'string' ? this.t(message)
      : this.t(message.key, typeof message.params === 'function' ? message.params() : message.params);
  }
  number(value: number, maximumFractionDigits = 1): string {
    return new Intl.NumberFormat(this.locale(), { maximumFractionDigits }).format(value);
  }
  duration(minutes: number): string {
    const total = Math.max(0, Math.floor(minutes));
    const hours = Math.floor(total / 60);
    return hours ? this.t('duration.hoursMinutes', { hours: this.number(hours, 0), minutes: this.number(total % 60, 0) })
      : this.t('duration.minutes', { minutes: this.number(total, 0) });
  }
  countLabel(noun: 'streams' | 'games' | 'hours' | 'chatAccounts' | 'uniqueChatAccounts' | 'minutes', count: number): string {
    const category = new Intl.PluralRules(this.locale()).select(count);
    const dictionary = DICTIONARIES[this.language()];
    return dictionary[('noun.' + noun + '.' + category) as TranslationKey] || dictionary[('noun.' + noun + '.other') as TranslationKey];
  }
  plural(key: 'streamerCount' | 'dayCount' | 'segmentCount' | 'broadcastDayCount', count: number): string {
    const category = new Intl.PluralRules(this.locale()).select(count);
    const forms = DICTIONARIES[this.language()];
    const form = (key + '.' + category) as TranslationKey;
    return (forms[form] || forms[(key + '.other') as TranslationKey]).replace('{count}', this.number(count, 0));
  }
}
