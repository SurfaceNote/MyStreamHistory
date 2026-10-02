import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ContentService } from './content.service';
import { ArticleSummary, ArticleType } from './content.models';

@Component({
  selector: 'app-public-content',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './public-content.component.html',
  styleUrl: './public-content.component.scss'
})
export class PublicContentComponent implements OnInit {
  readonly Math = Math;
  private readonly route = inject(ActivatedRoute);
  readonly content = inject(ContentService);
  type: ArticleType = 'news';
  articles: ArticleSummary[] = [];
  page = 1;
  total = 0;
  loading = true;
  error = false;

  get heading(): string { return this.type === 'news' ? 'News' : 'Reviews'; }
  ngOnInit(): void {
    this.route.data.subscribe(data => {
      this.type = data['type'] as ArticleType;
      this.load(1);
    });
  }
  load(page: number): void {
    this.loading = true;
    this.error = false;
    this.content.published(this.type, page).subscribe({
      next: result => { this.articles = result.items; this.total = result.total; this.page = result.page; this.loading = false; },
      error: () => { this.error = true; this.loading = false; }
    });
  }
}
