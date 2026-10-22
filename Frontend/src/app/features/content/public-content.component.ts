import { SeoService } from '../../service/seo.service';
import { ResponseStatusService } from '../../core/api/response-status.service';
import { LocaleService } from '../../i18n/locale.service';
import { TranslatePipe, LocalPathPipe } from '../../i18n/translate.pipe';
import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ContentService } from './content.service';
import { ArticleSummary, ArticleType } from './content.models';

@Component({
  selector: 'app-public-content',
  standalone: true,
  imports: [TranslatePipe, LocalPathPipe, CommonModule, RouterLink],
  templateUrl: './public-content.component.html',
  styleUrl: './public-content.component.scss'
})
export class PublicContentComponent implements OnInit {
  private readonly seo = inject(SeoService);
  private readonly responseStatus = inject(ResponseStatusService);
  readonly locale = inject(LocaleService);
  readonly Math = Math;
  private readonly route = inject(ActivatedRoute);
  readonly content = inject(ContentService);
  type: ArticleType = 'news';
  articles: ArticleSummary[] = [];
  page = 1;
  total = 0;
  loading = true;
  error = false;

  get heading(): string { return this.locale.t(this.type === 'news' ? 'ui.news' : 'ui.reviews'); }
  ngOnInit(): void {
    this.route.data.subscribe(data => {
      this.type = data['type'] as ArticleType;
      this.load(1);
    });
  }
  load(page: number): void {
    this.loading = true;
    this.error = false;
    this.content.published(this.type, page, this.locale.language()).subscribe({
      next: result => { this.responseStatus.reset(); this.seo.update({ title: this.heading + ' — MyStreamHistory', description: this.locale.t(this.type === 'news' ? 'seo.newsDescription' : 'seo.reviewsDescription') }); this.articles = result.items; this.total = result.total; this.page = result.page; this.loading = false; },
      error: error => { this.responseStatus.fail({ status: 502 }); this.error = true; this.loading = false; }
    });
  }
}
