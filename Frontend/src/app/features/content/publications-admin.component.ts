import { TranslatePipe } from '../../i18n/translate.pipe';
import { LocaleService, LocalizedMessage } from '../../i18n/locale.service';
import { LANGUAGES, Language } from '../../i18n/languages';
import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ContentService } from './content.service';
import { ArticleSummary, ArticleType } from './content.models';

@Component({
  selector: 'app-publications-admin',
  standalone: true,
  imports: [TranslatePipe, CommonModule, FormsModule, RouterLink],
  templateUrl: './publications-admin.component.html',
  styleUrl: './publications-admin.component.scss'
})
export class PublicationsAdminComponent implements OnInit {
  readonly locale = inject(LocaleService);
  private readonly content = inject(ContentService);
  private readonly router = inject(Router);
  items: ArticleSummary[] = [];
  readonly languages = LANGUAGES;
  language = '';
  createLanguage: Language = 'en';
  type = '';
  status = '';
  search = '';
  page = 1;
  total = 0;
  loading = false;
  creating = false;
  private _error: LocalizedMessage = '';
  get error(): string { return this.locale.message(this._error); }
  set error(value: LocalizedMessage) { this._error = value; }

  ngOnInit(): void { this.load(); }
  load(page = 1): void {
    this.page = page;
    this.loading = true;
    this.error = '';
    this.content.articles({ language: this.language, type: this.type, status: this.status, search: this.search, page }).subscribe({
      next: result => { this.items = result.items; this.total = result.total; this.loading = false; },
      error: () => { this.error = 'editor.loadPublicationsFailed'; this.loading = false; }
    });
  }
  create(type: ArticleType): void {
    this.creating = true;
    const slug = `untitled-${Date.now()}`;
    this.content.create({ language: this.createLanguage, type, slug, title: '', summary: '', seoTitle: '', seoDescription: '',
      coverId: null, revision: 1, body: { type: 'doc', content: [] } }).subscribe({
      next: article => this.router.navigate(['/admin/publications', article.id]),
      error: () => { this.error = 'editor.createDraftFailed'; this.creating = false; }
    });
  }
}
