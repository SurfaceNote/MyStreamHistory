import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { Article, ContentNode } from './content.models';
import { ContentService } from './content.service';
import { ContentRendererComponent } from './content-renderer.component';
import { SeoService } from '../../service/seo.service';

@Component({
  selector: 'app-article-preview', standalone: true,
  imports: [CommonModule, RouterLink, ContentRendererComponent],
  template: `<div class="preview-banner">Draft preview · Only you can see this <a [routerLink]="['/admin/publications', article?.id]">Back to editor</a></div>
    <main *ngIf="article as item" class="preview-page"><p class="category">{{ item.type === 'news' ? 'NEWS' : 'REVIEW' }}</p><h1>{{ item.title || 'Untitled draft' }}</h1><img *ngIf="cover" [src]="cover" [alt]="item.title" /><div class="body"><app-content-renderer [node]="body" /></div></main>
    <p *ngIf="error">Preview could not be loaded.</p>`,
  styles: [`.preview-banner { background:#513272; color:#fff; padding:12px 24px; display:flex; justify-content:space-between; } .preview-banner a { color:#fff; text-decoration:underline; } .preview-page { max-width:900px; margin:50px auto; padding:24px; } .category { color:#b98cff; font-size:.75rem; letter-spacing:.14em; font-weight:800; } h1 { font-size:clamp(2.4rem,5vw,4rem); } img { width:100%; border-radius:16px; margin:25px 0; } .body { max-width:760px; margin:auto; }`]
})
export class ArticlePreviewComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly content = inject(ContentService);
  private readonly seo = inject(SeoService);
  article: Article | null = null;
  body: ContentNode = { type: 'doc', content: [] };
  cover = '';
  error = false;
  private objectUrls: string[] = [];
  ngOnDestroy(): void { this.objectUrls.forEach(url => URL.revokeObjectURL(url)); }
  async ngOnInit(): Promise<void> {
    this.seo.update({ title: 'Draft preview — MyStreamHistory', description: 'Private article preview.', noIndex: true });
    try {
      const id = this.route.snapshot.paramMap.get('id')!;
      this.article = await firstValueFrom(this.content.article(id));
      this.body = structuredClone(this.article.body);
      await this.replaceImages(this.body);
      if (this.article.coverId) this.cover = this.objectUrl(await firstValueFrom(this.content.mediaBlob(this.article.coverId)));
    } catch { this.error = true; }
  }
  private async replaceImages(node: ContentNode): Promise<void> {
    if (node.type === 'image' && typeof node.attrs?.['src'] === 'string') {
      const id = /^\/content\/media\/([0-9a-f-]{36})$/i.exec(node.attrs['src'])?.[1];
      if (id) node.attrs['src'] = this.objectUrl(await firstValueFrom(this.content.mediaBlob(id)));
    }
    for (const child of node.content || []) await this.replaceImages(child);
  }
  private objectUrl(blob: Blob): string {
    const url = URL.createObjectURL(blob);
    this.objectUrls.push(url);
    return url;
  }
}
