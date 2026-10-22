import { ResponseStatusService } from './core/api/response-status.service';
import { isServicePath, unlocalizedPath } from './i18n/languages';
import { LocaleService } from './i18n/locale.service';
import { LanguageSuggestionComponent } from './i18n/language-suggestion.component';
import { TranslatePipe } from './i18n/translate.pipe';
import { Component, OnInit, effect, inject, untracked } from '@angular/core';
import { ActivatedRoute, NavigationStart, NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { HeaderComponent } from "./components/header/header.component";
import { filter } from 'rxjs/operators';
import { TranslationKey } from './i18n/translations';
import { SeoData, SeoService } from './service/seo.service';

@Component({
  selector: 'app-root',
  imports: [TranslatePipe, RouterOutlet, HeaderComponent, LanguageSuggestionComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
  readonly locale = inject(LocaleService);
  private router = inject(Router);
  private activatedRoute = inject(ActivatedRoute);
  private readonly status = inject(ResponseStatusService);
  private seo = inject(SeoService);

  get isOverlayRoute(): boolean {
    return this.router.url.startsWith('/overlay/');
  }

  get mainContentLink(): string {
    return this.router.url.split('#')[0] + '#main-content';
  }

  constructor() {
    effect(() => {
      this.locale.language();
      if (isServicePath(this.router.url)) untracked(() => this.updateRouteSeo());
    });
  }

  ngOnInit(): void {
    this.router.events.subscribe(event => {
      if (event instanceof NavigationStart) { this.status.reset(); this.seo.clearAlternates(); }
    });
    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(() => {
        // Instant scroll to top without animation
        if (typeof window !== 'undefined') window.scrollTo(0, 0);
        this.updateRouteSeo();
      });
  }

  private updateRouteSeo(): void {
        let route = this.activatedRoute;
        while (route.firstChild) {
          route = route.firstChild;
        }
        const seoData = route.snapshot.data['seo'] as SeoData | undefined;
        if (seoData) {
          const path = unlocalizedPath(this.router.url).split(/[?#]/)[0];
          let data = seoData;
          if (path === '/' || path === '') data = { ...data, title: this.locale.t('seo.homeTitle'), description: this.locale.t('seo.homeDescription') };
          if (path === '/news' || path === '/reviews') data = { ...data,
            title: this.locale.t(path === '/news' ? 'ui.news' : 'ui.reviews') + ' — MyStreamHistory',
            description: this.locale.t(path === '/news' ? 'seo.newsDescription' : 'seo.reviewsDescription') };
          const serviceSeo: Record<string, [TranslationKey, TranslationKey]> = {
            '/settings': ['seo.settingsTitle', 'seo.settingsDescription'],
            '/analytics/community': ['seo.communityTitle', 'seo.communityDescription'],
            '/callback': ['seo.callbackTitle', 'seo.callbackDescription'],
            '/admin': ['seo.diagnosticsTitle', 'seo.diagnosticsDescription'],
            '/admin/publications': ['seo.publicationsTitle', 'seo.publicationsDescription']
          };
          const keys = serviceSeo[path] || (/^\/admin\/publications\/[^/]+\/preview$/.test(path)
            ? ['seo.previewTitle', 'seo.previewDescription'] as const
            : /^\/admin\/publications\/[^/]+$/.test(path) ? ['seo.editorTitle', 'seo.editorDescription'] as const
            : path.startsWith('/overlay/') ? ['seo.overlayTitle', 'seo.overlayDescription'] as const : undefined);
          if (keys) data = { ...data, title: this.locale.t(keys[0]), description: this.locale.t(keys[1]) };
          this.seo.update(data);
        }
  }
}
