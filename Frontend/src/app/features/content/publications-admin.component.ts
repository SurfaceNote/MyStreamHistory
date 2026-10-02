import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ContentService } from './content.service';
import { ArticleSummary, ArticleType } from './content.models';

@Component({
  selector: 'app-publications-admin',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './publications-admin.component.html',
  styleUrl: './publications-admin.component.scss'
})
export class PublicationsAdminComponent implements OnInit {
  private readonly content = inject(ContentService);
  private readonly router = inject(Router);
  items: ArticleSummary[] = [];
  type = '';
  status = '';
  search = '';
  page = 1;
  total = 0;
  loading = false;
  creating = false;
  error = '';

  ngOnInit(): void { this.load(); }
  load(page = 1): void {
    this.page = page;
    this.loading = true;
    this.error = '';
    this.content.articles({ type: this.type, status: this.status, search: this.search, page }).subscribe({
      next: result => { this.items = result.items; this.total = result.total; this.loading = false; },
      error: () => { this.error = 'Could not load publications.'; this.loading = false; }
    });
  }
  create(type: ArticleType): void {
    this.creating = true;
    const slug = `untitled-${Date.now()}`;
    this.content.create({ type, slug, title: '', summary: '', seoTitle: '', seoDescription: '',
      coverId: null, revision: 1, body: { type: 'doc', content: [] } }).subscribe({
      next: article => this.router.navigate(['/admin/publications', article.id]),
      error: () => { this.error = 'Could not create a draft.'; this.creating = false; }
    });
  }
}
