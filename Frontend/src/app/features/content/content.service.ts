import { HttpClient, HttpParams, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE, ANONYMOUS_REQUEST } from '../../core/api/api-base';
import { Language } from '../../i18n/languages';
import { Article, ArticleInput, ArticleSummary, ArticleType, MediaAsset, PageResult, PublicArticle } from './content.models';

export const CONTENT_API_BASE = API_BASE;

@Injectable({ providedIn: 'root' })
export class ContentService {
  private readonly base = `${inject(CONTENT_API_BASE)}/content`;
  constructor(private http: HttpClient) {}

  mediaUrl(id: string): string { return `/content/media/${id}`; }

  published(type: ArticleType, page = 1, language: Language = 'en'): Observable<PageResult<ArticleSummary>> {
    return this.http.get<PageResult<ArticleSummary>>(this.base, { params: { type, page, language }, context: new HttpContext().set(ANONYMOUS_REQUEST, true) });
  }

  publicArticle(type: ArticleType, slug: string, language: Language = 'en'): Observable<PublicArticle> {
    return this.http.get<PublicArticle>(`${this.base}/${type}/${encodeURIComponent(slug)}`, { params: { language }, context: new HttpContext().set(ANONYMOUS_REQUEST, true) });
  }

  articles(filters: { language?: string; type?: string; status?: string; search?: string; page?: number }): Observable<PageResult<ArticleSummary>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filters)) if (value) params = params.set(key, value);
    return this.http.get<PageResult<ArticleSummary>>(`${this.base}/admin/articles`, { params });
  }

  createTranslation(id: string, language: Language): Observable<Article> {
    return this.http.post<Article>(`${this.base}/admin/articles/${id}/translations`, { language });
  }
  article(id: string): Observable<Article> { return this.http.get<Article>(`${this.base}/admin/articles/${id}`); }
  create(input: ArticleInput): Observable<Article> { return this.http.post<Article>(`${this.base}/admin/articles`, input); }
  save(id: string, input: ArticleInput): Observable<Article> { return this.http.put<Article>(`${this.base}/admin/articles/${id}`, input); }
  publish(id: string, revision: number): Observable<Article> { return this.http.post<Article>(`${this.base}/admin/articles/${id}/publish`, revision); }
  unpublish(id: string, revision: number): Observable<Article> { return this.http.post<Article>(`${this.base}/admin/articles/${id}/unpublish`, revision); }
  media(): Observable<MediaAsset[]> { return this.http.get<MediaAsset[]>(`${this.base}/admin/media`); }
  deleteMedia(id: string): Observable<void> { return this.http.delete<void>(`${this.base}/admin/media/${id}`); }
  mediaBlob(id: string): Observable<Blob> { return this.http.get(`${this.base}/media/${id}`, { responseType: 'blob' }); }
  upload(file: File): Observable<MediaAsset> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<MediaAsset>(`${this.base}/admin/media`, form);
  }
}
