import { DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { combineLatest, switchMap, catchError, EMPTY } from 'rxjs';
import { ResponseStatusService } from '../../core/api/response-status.service';
import { LocaleService } from '../../i18n/locale.service';
import { TranslatePipe, LocalPathPipe } from '../../i18n/translate.pipe';
import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ContentService } from './content.service';
import { ArticleType, PublicArticle } from './content.models';
import { ContentRendererComponent } from './content-renderer.component';
import { SeoService } from '../../service/seo.service';

@Component({
  selector: 'app-article-page',
  standalone: true,
  imports: [TranslatePipe, LocalPathPipe, CommonModule, RouterLink, ContentRendererComponent],
  templateUrl: './article-page.component.html',
  styleUrl: './article-page.component.scss'
})
export class ArticlePageComponent implements OnInit {
  readonly locale = inject(LocaleService);
  private route = inject(ActivatedRoute);
  readonly content = inject(ContentService);
  private seo = inject(SeoService);
  private readonly status = inject(ResponseStatusService);
  private readonly destroy = inject(DestroyRef);
  article: PublicArticle | null = null;
  loading = true;
  missing = false;
  type: ArticleType = 'news';
  ngOnInit(): void {
    combineLatest([this.route.data, this.route.paramMap]).pipe(
      switchMap(([data, params]) => {
        this.type = data['type'] as ArticleType;
        this.loading = true; this.article = null; this.missing = false;
        return this.content.publicArticle(this.type, params.get('slug')!, this.locale.language()).pipe(catchError(error => {
          this.status.fail(error); this.loading = false; this.missing = true;
          this.seo.update({ title: this.locale.t('ui.articleUnavailable') + ' — MyStreamHistory',
            description: this.locale.t('ui.theRequestedArticleIsUnavailable'), noIndex: true });
          return EMPTY;
        }));
      }), takeUntilDestroyed(this.destroy)
    ).subscribe({
      next: article => {
        this.status.reset();
        this.article = article; this.loading = false;
        const alternatives = article.translations.map(item => ({ language: item.language, path: item.path }));
        this.locale.alternatives.set(alternatives);
        this.seo.update({ title: article.seoTitle || article.title, description: article.seoDescription || article.summary,
          image: article.coverUrl || (article.coverId ? this.content.mediaUrl(article.coverId) : undefined),
          type: 'article', alternatives });
      },
      error: error => {
        this.status.fail(error); this.loading = false; this.missing = true;
        this.seo.update({ title: this.locale.t('ui.articleUnavailable') + ' — MyStreamHistory',
          description: this.locale.t('ui.theRequestedArticleIsUnavailable'), noIndex: true });
      }
    });
  }
}
