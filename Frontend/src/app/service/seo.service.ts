import { ResponseStatusService } from '../core/api/response-status.service';
import { Router } from '@angular/router';
import { LocaleService, LanguageAlternative } from '../i18n/locale.service';
import { LANGUAGES, localizedPath, unlocalizedPath, isPublicPath } from '../i18n/languages';
import { DOCUMENT } from '@angular/common';
import { inject, Injectable } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { environment } from '../../environments/environment';

export interface SeoData {
  title: string;
  description: string;
  image?: string;
  noIndex?: boolean;
  type?: 'website' | 'profile' | 'article';
  alternatives?: LanguageAlternative[];
}

@Injectable({ providedIn: 'root' })
export class SeoService {
  private readonly titleService = inject(Title);
  private readonly meta = inject(Meta);
  private readonly document = inject(DOCUMENT);

  private readonly router = inject(Router);
  private readonly locale = inject(LocaleService);
  clearAlternates(): void {
    this.document.head.querySelectorAll('link[rel="alternate"][hreflang]').forEach(link => link.remove());
    this.document.head.querySelectorAll('meta[property="og:locale:alternate"]').forEach(tag => tag.remove());
  }
  private readonly status = inject(ResponseStatusService);
  update(data: SeoData): void {
    if (this.status.failed()) data = { ...data, noIndex: true };
    const url = this.canonicalUrl();
    const image = this.absoluteUrl(data.image || '/logo.png');

    this.titleService.setTitle(data.title);
    this.meta.updateTag({ name: 'description', content: data.description });
    this.meta.updateTag({ name: 'robots', content: data.noIndex ? 'noindex, nofollow' : 'index, follow' });

    this.meta.updateTag({ property: 'og:type', content: data.type || 'website' });
    this.meta.updateTag({ property: 'og:site_name', content: 'MyStreamHistory' });
    this.meta.updateTag({ property: 'og:title', content: data.title });
    this.meta.updateTag({ property: 'og:description', content: data.description });
    this.meta.updateTag({ property: 'og:url', content: url });
    this.meta.updateTag({ property: 'og:image', content: image });
    this.meta.updateTag({ property: 'og:image:alt', content: this.locale.t('preview.alt', { title: data.title }) });

    this.meta.updateTag({ name: 'twitter:card', content: 'summary' });
    this.meta.updateTag({ name: 'twitter:title', content: data.title });
    this.meta.updateTag({ name: 'twitter:description', content: data.description });
    this.meta.updateTag({ name: 'twitter:image', content: image });

    this.setCanonical(url);
    this.document.documentElement.lang = this.locale.language();
    const language = LANGUAGES.find(item => item.code === this.locale.language())!;
    this.meta.updateTag({ property: 'og:locale', content: language.og });
    this.clearAlternates();
    const path = this.router.url.split(/[?#]/)[0];
    const article = /^\/(news|reviews)\/[^/]+/.test(unlocalizedPath(path));
    const alternatives = data.noIndex || !isPublicPath(path) ? [] : data.alternatives ??
      (article ? [] : LANGUAGES.map(item => ({ language: item.code, path: localizedPath(unlocalizedPath(path), item.code) })));
    for (const alternative of alternatives) {
      const link = this.document.createElement('link');
      link.rel = 'alternate'; link.hreflang = alternative.language; link.href = this.absoluteUrl(alternative.path);
      this.document.head.appendChild(link);
      if (alternative.language !== this.locale.language()) {
        const tag = this.document.createElement('meta');
        tag.setAttribute('property', 'og:locale:alternate');
        tag.content = LANGUAGES.find(item => item.code === alternative.language)!.og;
        this.document.head.appendChild(tag);
      }
    }
    const english = alternatives.find(item => item.language === 'en');
    if (english) {
      const link = this.document.createElement('link');
      link.rel = 'alternate'; link.hreflang = 'x-default'; link.href = this.absoluteUrl(english.path);
      this.document.head.appendChild(link);
    }
  }

  private canonicalUrl(): string {
    const path = this.router.url.split(/[?#]/)[0] || '/';
    return this.absoluteUrl(/^\/(ru|cs|de|es)\/?$/.test(path) ? path.replace(/\/?$/, '/') : path === '/' ? '/' : path.replace(/\/$/, ''));
  }

  private absoluteUrl(value: string): string {
    try {
      return new URL(value, `${environment.url}/`).toString();
    } catch {
      return `${environment.url}/`;
    }
  }

  private setCanonical(url: string): void {
    let link = this.document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');
    if (!link) {
      link = this.document.createElement('link');
      link.rel = 'canonical';
      this.document.head.appendChild(link);
    }
    link.href = url;
  }
}
