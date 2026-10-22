import { Injectable, afterNextRender, computed, inject, signal } from '@angular/core';
import { NavigationEnd, NavigationStart, Router } from '@angular/router';
import { ResponseStatusService } from '../core/api/response-status.service';
import { Language, isLanguage, unlocalizedPath } from './languages';
import { LocaleService } from './locale.service';

export const DISMISSED_LANGUAGE_SUGGESTIONS = 'dismissed_language_suggestions';
export interface LanguageSuggestion { language: Language; path: string; article: boolean; }

@Injectable({ providedIn: 'root' })
export class LanguageSuggestionService {
  private readonly locale = inject(LocaleService);
  private readonly router = inject(Router);
  private readonly status = inject(ResponseStatusService);
  private readonly url = signal(this.router.url);
  private readonly ready = signal(false);
  private readonly navigating = signal(!this.router.navigated);
  private readonly dismissed = signal<ReadonlySet<string>>(new Set());

  readonly offer = computed<LanguageSuggestion | null>(() => {
    if (!this.ready() || this.navigating() || this.status.failed()) return null;
    const url = this.url();
    // Bare '/' already has automatic language negotiation.
    if (url.split(/[?#]/)[0] === '/') return null;
    const path = unlocalizedPath(url).split(/[?#]/)[0];
    const article = /^\/(news|reviews)\/[^/]+\/?$/.test(path);
    const sharedPage = /^\/(?:$|news\/?$|reviews\/?$|profile\/[^/]+(?:\/viewer\/[^/]+)?\/?$|stream\/[^/]+\/?$)/.test(path);
    if (!article && !sharedPage) return null;
    const preferences = this.locale.preferenceLanguages();
    const alternatives = this.locale.alternatives();
    const language = preferences.find(language => !article || alternatives?.some(item => item.language === language));
    // The current version may already be the first available preferred language.
    if (!language || language === this.locale.language()) return null;
    if (this.dismissed().has(this.pair(language))) return null;
    return { language, article, path: this.locale.switchPath(language) };
  });

  constructor() {
    // Client-only preference UI keeps SSR content and hydration independent of personal storage.
    afterNextRender(() => {
      try {
        const stored: unknown = JSON.parse(sessionStorage.getItem(DISMISSED_LANGUAGE_SUGGESTIONS) || '[]');
        if (Array.isArray(stored)) this.dismissed.set(new Set(stored.filter(this.validPair)));
      } catch { /* Session storage is optional. */ }
      this.ready.set(true);
    });
    this.router.events.subscribe(event => {
      if (event instanceof NavigationStart) { this.navigating.set(true); this.url.set(event.url); }
      if (event instanceof NavigationEnd) { this.url.set(event.urlAfterRedirects); this.navigating.set(false); }
    });
  }

  private pair(language: Language): string { return `${this.locale.language()}:${language}`; }
  private validPair(value: unknown): value is string {
    if (typeof value !== 'string') return false;
    const parts = value.split(':');
    return parts.length === 2 && isLanguage(parts[0]) && isLanguage(parts[1]) && parts[0] !== parts[1];
  }
  dismiss(): void {
    const offer = this.offer();
    if (!offer) return;
    const dismissed = new Set(this.dismissed());
    dismissed.add(this.pair(offer.language));
    this.dismissed.set(dismissed);
    try { sessionStorage.setItem(DISMISSED_LANGUAGE_SUGGESTIONS, JSON.stringify([...dismissed])); }
    catch { /* In-memory dismissal still works when storage is blocked. */ }
  }
}
