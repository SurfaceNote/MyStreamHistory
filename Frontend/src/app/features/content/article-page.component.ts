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
  imports: [CommonModule, RouterLink, ContentRendererComponent],
  templateUrl: './article-page.component.html',
  styleUrl: './article-page.component.scss'
})
export class ArticlePageComponent implements OnInit {
  private route = inject(ActivatedRoute);
  readonly content = inject(ContentService);
  private seo = inject(SeoService);
  article: PublicArticle | null = null;
  loading = true;
  missing = false;
  type: ArticleType = 'news';
  ngOnInit(): void {
    this.route.data.subscribe(data => {
      this.type = data['type'] as ArticleType;
      this.route.paramMap.subscribe(params => {
        const slug = params.get('slug');
        if (!slug) return;
        this.loading = true;
        this.article = null;
        this.content.publicArticle(this.type, slug).subscribe({
          next: article => {
            this.article = article;
            this.loading = false;
            this.missing = false;
            this.seo.update({ title: article.seoTitle || article.title, description: article.seoDescription || article.summary,
              image: article.coverId ? this.content.mediaUrl(article.coverId) : undefined, type: 'article' });
          },
          error: () => { this.loading = false; this.missing = true; this.seo.update({ title: 'Article unavailable — MyStreamHistory', description: 'The requested article is unavailable.', noIndex: true }); }
        });
      });
    });
  }
}
