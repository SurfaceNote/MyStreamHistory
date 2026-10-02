import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, ElementRef, HostListener, OnDestroy, OnInit, PLATFORM_ID, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Editor } from '@tiptap/core';
import StarterKit from '@tiptap/starter-kit';
import Image from '@tiptap/extension-image';
import { Subject, debounceTime, firstValueFrom, takeUntil } from 'rxjs';
import { ContentService } from './content.service';
import { Article, ArticleInput, ContentNode, MediaAsset } from './content.models';
import { PrivateMediaThumbComponent } from './private-media-thumb.component';

@Component({
  selector: 'app-article-editor',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, PrivateMediaThumbComponent],
  templateUrl: './article-editor.component.html',
  styleUrl: './article-editor.component.scss'
})
export class ArticleEditorComponent implements OnInit, OnDestroy {
  private readonly content = inject(ContentService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  @ViewChild('editorHost')
  set editorHost(host: ElementRef<HTMLElement> | undefined) {
    if (host && this.browser && !this.editor) this.initializeEditor(host.nativeElement);
  }
  editorReady = false;
  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;
  editor: Editor | null = null;
  article: Article | null = null;
  media: MediaAsset[] = [];
  coverPreview = '';
  mediaOpen = false;
  linkOpen = false;
  linkUrl = '';
  slugEdited = false;
  loading = true;
  busy = false;
  saveState = 'Saved';
  error = '';
  private changes = new Subject<void>();
  private changeSeq = 0;
  private savedSeq = 0;
  private savePromise: Promise<void> | null = null;
  private blobById = new Map<string, string>();
  private idByBlob = new Map<string, string>();
  private destroyed = false;
  private readonly stop = new Subject<void>();

  ngOnInit(): void {
    this.changes.pipe(debounceTime(1200), takeUntil(this.stop)).subscribe(() => void this.flush());
    this.route.paramMap.pipe(takeUntil(this.stop)).subscribe(params => {
      const id = params.get('id');
      if (id) this.load(id);
    });
  }

  private initializeEditor(element: HTMLElement): void {
    this.editor = new Editor({
      element,
      editable: false,
      extensions: [StarterKit.configure({
        heading: { levels: [2, 3] },
        code: false,
        codeBlock: false,
        strike: false,
        underline: false,
        horizontalRule: false,
        link: { openOnClick: false }
      }), Image],
      content: { type: 'doc', content: [] },
      onUpdate: () => this.changed(),
      editorProps: {
        attributes: { class: 'writing-surface', 'aria-label': 'Article body' },
        handleDrop: (_view, event) => {
          const file = event.dataTransfer?.files?.[0];
          if (!file) return false;
          event.preventDefault();
          void this.upload(file, true);
          return true;
        },
        handlePaste: (_view, event) => {
          const file = event.clipboardData?.files?.[0];
          if (!file) return false;
          event.preventDefault();
          void this.upload(file, true);
          return true;
        }
      }
    });
    if (this.article) void this.setEditorBody(this.article.body).catch(() => {
      this.error = 'Could not open the article body. Reload before editing.';
    });
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.stop.next();
    this.stop.complete();
    this.changes.complete();
    this.editor?.destroy();
    for (const url of this.blobById.values()) URL.revokeObjectURL(url);
  }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (this.changeSeq !== this.savedSeq) event.preventDefault();
  }

  private async load(id: string): Promise<void> {
    this.loading = true;
    this.editorReady = false;
    this.editor?.setEditable(false, false);
    this.error = '';
    this.coverPreview = '';
    try {
      this.article = await firstValueFrom(this.content.article(id));
      this.slugEdited = !this.article.slug.startsWith('untitled-');
      this.changeSeq = 0;
      this.savedSeq = 0;
      this.saveState = 'Saved';
      await this.setEditorBody(this.article.body);
      if (this.article.coverId) this.coverPreview = await this.resolveMedia(this.article.coverId);
      this.loadMedia();
    } catch { this.article = null; this.error = 'Could not load this draft. Return to Publications and try again.'; }
    finally { this.loading = false; }
  }

  private async setEditorBody(body: ContentNode): Promise<void> {
    if (!this.editor) return;
    const copy = structuredClone(body);
    await this.walkImages(copy, async src => {
      const match = /^\/content\/media\/([0-9a-f-]{36})$/i.exec(src);
      return match ? this.resolveMedia(match[1]) : src;
    });
    if (!this.destroyed) {
      this.editor.commands.setContent(copy, { emitUpdate: false });
      this.editor.setEditable(true, false);
      this.editorReady = true;
    }
  }

  private async walkImages(node: ContentNode, map: (src: string) => Promise<string>): Promise<void> {
    if (node.type === 'image' && typeof node.attrs?.['src'] === 'string') node.attrs['src'] = await map(node.attrs['src']);
    for (const child of node.content || []) await this.walkImages(child, map);
  }

  private async resolveMedia(id: string): Promise<string> {
    const cached = this.blobById.get(id);
    if (cached) return cached;
    const blob = await firstValueFrom(this.content.mediaBlob(id));
    const url = URL.createObjectURL(blob);
    this.blobById.set(id, url);
    this.idByBlob.set(url, id);
    return url;
  }

  loadMedia(): void {
    this.content.media().subscribe({ next: items => this.media = items, error: () => this.error = 'Could not load media library.' });
  }

  changed(): void {
    this.changeSeq++;
    this.saveState = 'Unsaved changes';
    this.changes.next();
  }

  titleChanged(): void {
    if (this.article && !this.article.slugLocked && !this.slugEdited && this.article.title.trim())
      this.article.slug = this.article.title.toLowerCase().normalize('NFKD').replace(/[^a-z0-9]+/g, '-').slice(0, 90).replace(/^-|-$/g, '') || this.article.slug;
    this.changed();
  }

  link(): void { this.linkOpen = !this.linkOpen; }
  applyLink(): void {
    if (!/^https?:\/\//i.test(this.linkUrl)) { this.error = 'Links must start with http:// or https://.'; return; }
    this.editor?.chain().focus().setLink({ href: this.linkUrl }).run();
    this.linkOpen = false;
    this.linkUrl = '';
    this.error = '';
  }

  private snapshot(): ArticleInput {
    const article = this.article!;
    const body = structuredClone(this.editor?.getJSON() || article.body) as ContentNode;
    const normalize = (node: ContentNode): void => {
      if (node.type === 'image' && typeof node.attrs?.['src'] === 'string') {
        const id = this.idByBlob.get(node.attrs['src']);
        if (id) node.attrs['src'] = this.content.mediaUrl(id);
      }
      node.content?.forEach(normalize);
    };
    normalize(body);
    return { type: article.type, slug: article.slug, title: article.title, summary: article.summary,
      seoTitle: article.seoTitle, seoDescription: article.seoDescription, coverId: article.coverId,
      body, revision: article.revision };
  }

  async flush(): Promise<boolean> {
    if (!this.article || this.changeSeq === this.savedSeq) return true;
    if (!this.editorReady) {
      this.error = 'Wait for the article body to finish loading before saving.';
      return false;
    }
    if (this.savePromise) { await this.savePromise; return this.error ? false : this.flush(); }
    const sequence = this.changeSeq;
    const payload = this.snapshot();
    this.saveState = 'Saving…';
    this.savePromise = (async () => {
      try {
        const saved = await firstValueFrom(this.content.save(this.article!.id, payload));
        if (this.article) { this.article.revision = saved.revision; this.article.slugLocked = saved.slugLocked; }
        this.savedSeq = sequence;
        this.saveState = this.changeSeq === sequence ? 'Saved' : 'Unsaved changes';
        this.error = '';
      } catch (error: any) {
        this.saveState = 'Save failed';
        this.error = this.requestError(error, 'Draft could not be saved. Retry before leaving.');
      }
    })();
    await this.savePromise;
    this.savePromise = null;
    if (this.changeSeq > this.savedSeq && !this.error) return this.flush();
    return this.changeSeq === this.savedSeq;
  }

  async preview(): Promise<void> {
    if (await this.flush() && this.article) void this.router.navigate(['/admin/publications', this.article.id, 'preview']);
  }

  get publicationRequirements(): string[] {
    if (!this.article) return [];
    const missing: string[] = [];
    if (!this.article.title.trim()) missing.push('headline');
    if (!this.article.coverId) missing.push('cover image');
    if (!this.editorReady || this.editor?.isEmpty) missing.push('article text');
    return missing;
  }

  private requestError(error: any, fallback: string): string {
    if (typeof error?.error === 'string' && error.error.trim()) return error.error;
    if (error?.status === 401 || error?.status === 403) return 'Your session expired or you do not have publishing access. Keep this tab open and sign in again.';
    if (error?.status === 0) return 'The server could not be reached. Keep this tab open and retry saving when the connection returns.';
    return error?.error?.detail || error?.error?.title || fallback;
  }

  async publish(): Promise<void> {
    if (this.publicationRequirements.length) {
      this.error = `Before publishing, add: ${this.publicationRequirements.join(', ')}. You can save an incomplete draft.`;
      return;
    }
    if (!await this.flush() || !this.article) return;
    this.busy = true;
    this.error = '';
    try {
      const saved = await firstValueFrom(this.content.publish(this.article.id, this.article.revision));
      this.article.revision = saved.revision;
      this.article.publishedAt = saved.publishedAt;
      this.article.slugLocked = true;
      this.saveState = 'Published';
    } catch (error: any) { this.error = this.requestError(error, 'Could not publish. Check the title, cover and body.'); }
    finally { this.busy = false; }
  }

  async unpublish(): Promise<void> {
    if (!await this.flush() || !this.article) return;
    this.busy = true;
    try {
      const saved = await firstValueFrom(this.content.unpublish(this.article.id, this.article.revision));
      this.article.revision = saved.revision;
      this.article.publishedAt = null;
      this.saveState = 'Unpublished';
    } catch { this.error = 'Could not unpublish this article.'; }
    finally { this.busy = false; }
  }

  chooseFile(): void { this.fileInput.nativeElement.click(); }
  onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.[0]) void this.upload(input.files[0], false);
    input.value = '';
  }

  async upload(file: File, insert: boolean): Promise<void> {
    this.busy = true;
    this.error = '';
    try {
      const asset = await firstValueFrom(this.content.upload(file));
      this.media.unshift(asset);
      if (insert) await this.insertImage(asset);
      else await this.setCover(asset);
    } catch (error: any) { this.error = typeof error?.error === 'string' ? error.error : 'Upload failed. Use JPEG, PNG, WebP or GIF within the size limit.'; }
    finally { this.busy = false; }
  }

  async insertImage(asset: MediaAsset): Promise<void> {
    try {
      const url = await this.resolveMedia(asset.id);
      this.editor?.chain().focus().setImage({ src: url, alt: asset.fileName }).run();
      this.mediaOpen = false;
    } catch { this.error = 'Image uploaded, but could not be opened. Try selecting it again.'; }
  }

  async setCover(asset: MediaAsset): Promise<void> {
    if (!this.article) return;
    try {
      this.coverPreview = await this.resolveMedia(asset.id);
      this.article.coverId = asset.id;
      this.changed();
      this.mediaOpen = false;
    } catch { this.error = 'Could not open cover image.'; }
  }
}
