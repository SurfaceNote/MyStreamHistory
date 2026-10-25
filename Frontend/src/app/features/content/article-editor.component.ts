import { TranslationKey } from '../../i18n/translations';
import { TranslatePipe } from '../../i18n/translate.pipe';
import { LocaleService, LocalizedMessage } from '../../i18n/locale.service';
import { articleSlug } from './article-slug';
import { LANGUAGES, Language } from '../../i18n/languages';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, effect, ElementRef, HostListener, OnDestroy, OnInit, PLATFORM_ID, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Editor } from '@tiptap/core';
import StarterKit from '@tiptap/starter-kit';
import Image from '@tiptap/extension-image';
import { Selection } from '@tiptap/pm/state';
import { Subject, debounceTime, firstValueFrom, takeUntil } from 'rxjs';
import { ContentService } from './content.service';
import { Article, ArticleInput, ContentNode, MediaAsset } from './content.models';
import { PrivateMediaThumbComponent } from './private-media-thumb.component';
import { YoutubeVideo } from './youtube.extension';
import { youtubeVideoId } from './youtube';

@Component({
  selector: 'app-article-editor',
  standalone: true,
  imports: [TranslatePipe, CommonModule, FormsModule, RouterLink, PrivateMediaThumbComponent],
  templateUrl: './article-editor.component.html',
  styleUrl: './article-editor.component.scss'
})
export class ArticleEditorComponent implements OnInit, OnDestroy {
  readonly locale = inject(LocaleService);
  private readonly content = inject(ContentService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  @ViewChild('editorHost')
  set editorHost(host: ElementRef<HTMLElement> | undefined) {
    if (host && this.browser && !this.editor) this.initializeEditor(host.nativeElement);
  }
  readonly languages = LANGUAGES;
  translationLanguage: Language = 'ru';
  editorReady = false;
  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;
  editor: Editor | null = null;
  article: Article | null = null;
  media: MediaAsset[] = [];
  coverPreview = '';
  mediaOpen = false;
  linkOpen = false;
  linkUrl = '';
  youtubeOpen = false;
  youtubeUrl = '';
  private _youtubeError: LocalizedMessage = '';
  get youtubeError(): string { return this.locale.message(this._youtubeError); }
  set youtubeError(value: LocalizedMessage) { this._youtubeError = value; }
  slugEdited = false;
  loading = true;
  busy = false;
  restoring = false;
  saveState = 'Saved';
  get saveStateLabel(): string {
    const labels: Record<string, TranslationKey> = { 'Saved': 'editor.saved', 'Unsaved changes': 'editor.unsaved', 'Saving…': 'editor.saving', 'Save failed': 'editor.saveFailed', 'Published': 'editor.published', 'Unpublished': 'editor.unpublished' };
    return this.locale.t(labels[this.saveState] || 'editor.saved');
  }
  private _error: LocalizedMessage = '';
  get error(): string { return this.locale.message(this._error); }
  set error(value: LocalizedMessage) { this._error = value; }
  private changes = new Subject<void>();
  private changeSeq = 0;
  private savedSeq = 0;
  private savePromise: Promise<void> | null = null;
  private deleting = false;
  private blobById = new Map<string, string>();
  private idByBlob = new Map<string, string>();
  private destroyed = false;
  private readonly stop = new Subject<void>();

  constructor() {
    effect(() => {
      this.locale.language();
      this.editor?.view.dom.setAttribute('aria-label', this.locale.t('editor.body'));
    });
  }

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
      }), Image, YoutubeVideo.configure({ labels: () => ({ invalid: this.locale.t('editor.invalidVideo'), player: this.locale.t('editor.youtubePlayer') }) })],
      content: { type: 'doc', content: [] },
      onUpdate: () => this.changed(),
      editorProps: {
        attributes: { class: 'writing-surface', 'aria-label': this.locale.t('editor.body') },
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
      this.error = 'editor.openBodyFailed';
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
      this.article.translations ??= [];
      this.translationLanguage = this.languages.find(lang => !this.translationFor(lang.code))?.code || 'en';
      this.slugEdited = !this.article.slug.startsWith('untitled-');
      this.changeSeq = 0;
      this.savedSeq = 0;
      this.saveState = 'Saved';
      await this.setEditorBody(this.article.body);
      if (this.article.coverId) this.coverPreview = await this.resolveMedia(this.article.coverId);
      this.loadMedia();
    } catch { this.article = null; this.error = 'editor.loadDraftFailed'; }
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
      this.editor.commands.command(({ tr }) => {
        tr.setSelection(Selection.atStart(tr.doc));
        return true;
      });
      this.editor.setEditable(!this.restoring, false);
      this.editor.view.dom.setAttribute('lang', this.article?.language || 'en');
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
    this.content.media().subscribe({ next: items => this.media = items, error: () => this.error = 'editor.loadMediaFailed' });
  }

  changed(): void {
    this.changeSeq++;
    this.saveState = 'Unsaved changes';
    this.changes.next();
  }

  titleChanged(): void {
    if (this.article && !this.article.slugLocked && !this.slugEdited && this.article.title.trim())
      this.article.slug = articleSlug(this.article.title) || this.article.slug;
    this.changed();
  }

  link(): void { this.linkOpen = !this.linkOpen; }
  insertYoutube(): void {
    if (!this.editorReady || !this.editor || this.busy) return;
    const videoId = youtubeVideoId(this.youtubeUrl);
    if (!videoId) {
      this.youtubeError = 'editor.invalidYoutube';
      return;
    }
    if (this.editor.chain().focus().command(({ tr, state, dispatch }) => {
      // ProseMirror preserves text on both sides when splitting a paragraph for a block.
      if (dispatch) tr.replaceSelectionWith(state.schema.nodes['youtube'].create({ videoId }));
      return true;
    }).command(({ commands }) => {
      commands.createParagraphNear();
      return true;
    }).run()) {
      this.youtubeOpen = false;
      this.youtubeUrl = '';
      this.youtubeError = '';
    }
  }

  applyLink(): void {
    if (!/^https?:\/\//i.test(this.linkUrl)) { this.error = 'editor.invalidLink'; return; }
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
    return { language: article.language, type: article.type, slug: article.slug, title: article.title, summary: article.summary,
      seoTitle: article.seoTitle, seoDescription: article.seoDescription, coverId: article.coverId,
      body, revision: article.revision };
  }

  async flush(): Promise<boolean> {
    if (!this.article) return true;
    if (this.deleting || this.restoring) return false;
    if (this.changeSeq === this.savedSeq) return true;
    if (!this.editorReady) {
      this.error = 'editor.waitForBody';
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
        this.error = this.requestError(error, 'editor.saveDraftFailed');
      }
    })();
    await this.savePromise;
    this.savePromise = null;
    if (this.changeSeq > this.savedSeq && !this.error) return this.flush();
    return this.changeSeq === this.savedSeq;
  }

  translationFor(language: Language) { return this.article?.translations?.find(item => item.language === language); }
  async createTranslation(): Promise<void> {
    if (!await this.flush() || !this.article || this.busy) return;
    this.busy = true; this.error = '';
    try {
      const translation = await firstValueFrom(this.content.createTranslation(this.article.id, this.translationLanguage));
      await this.router.navigate(['/admin/publications', translation.id]);
    } catch (error: any) { this.error = this.requestError(error, 'editor.createTranslationFailed'); }
    finally { this.busy = false; }
  }

  async preview(): Promise<void> {
    if (await this.flush() && this.article) void this.router.navigate(['/admin/publications', this.article.id, 'preview']);
  }

  get publicationRequirements(): string[] {
    if (!this.article) return [];
    const missing: string[] = [];
    if (!this.article.title.trim()) missing.push(this.locale.t('editor.headline'));
    if (!this.article.coverId) missing.push(this.locale.t('editor.coverRequirement'));
    if (!this.editorReady || this.editor?.isEmpty) missing.push(this.locale.t('editor.bodyRequirement'));
    return missing;
  }

  private requestError(error: any, fallback: TranslationKey): LocalizedMessage {
    const backendMessage = typeof error?.error === 'string' ? error.error : error?.error?.detail || error?.error?.title;
    const knownErrors: Record<string, TranslationKey> = {
      'Article URL is already in use.': 'editor.urlInUse',
      'Use a lowercase URL slug with letters, numbers and hyphens.': 'editor.invalidSlug',
      'This article has no published version to restore.': 'editor.noPublishedVersion',
      'This image is used in an article.': 'editor.imageUsed',
      'Image in use': 'editor.imageInUse',
      'This image is used in a draft or published article. Remove it from all articles and update any published versions before deleting.': 'editor.imageUsedDetailed',
      'This article was changed in another tab. Reload before saving.': 'editor.revisionConflict',
      'This article was changed in another tab.': 'editor.revisionConflict',
      'A translation in this language already exists.': 'editor.translationExists',
      'Image exceeds the size limit.': 'editor.imageTooLarge'
    };
    if (knownErrors[backendMessage]) return knownErrors[backendMessage];
    if (error?.status === 401 || error?.status === 403) return 'editor.sessionExpired';
    if (error?.status === 0) return 'editor.connectionFailed';
    return fallback;
  }

  async publish(): Promise<void> {
    if (this.publicationRequirements.length) {
      this.error = { key: 'editor.requirementsError', params: () => ({ fields: this.publicationRequirements.join(', ') }) };
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
    } catch (error: any) { this.error = this.requestError(error, 'editor.publishFailed'); }
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
    } catch { this.error = 'editor.unpublishFailed'; }
    finally { this.busy = false; }
  }

  async restoreDraft(): Promise<void> {
    if (!this.article?.publishedAt || this.busy || !this.browser) return;
    if (!window.confirm(this.locale.t('editor.restoreDraftConfirm'))) return;
    this.busy = true;
    this.restoring = true;
    this.error = '';
    this.editor?.setEditable(false, false);
    try {
      // Stop autosave from putting discarded edits back after the restored version.
      if (this.savePromise) await this.savePromise;
      const restored = await firstValueFrom(this.content.restoreDraft(this.article.id, this.article.revision));
      this.article = restored;
      this.slugEdited = true;
      this.savedSeq = this.changeSeq;
      this.saveState = 'Saved';
      this.error = '';
      this.editorReady = false;
      this.coverPreview = '';
      this.linkOpen = this.youtubeOpen = this.mediaOpen = false;
      try {
        await this.setEditorBody(restored.body);
        if (restored.coverId) this.coverPreview = await this.resolveMedia(restored.coverId);
      } catch {
        this.editorReady = false;
        this.error = 'editor.openBodyFailed';
      }
    } catch (error: any) { this.error = this.requestError(error, 'editor.restoreDraftFailed'); }
    finally {
      this.restoring = false;
      this.busy = false;
      this.editor?.setEditable(this.editorReady, false);
    }
  }

  async deleteArticle(): Promise<void> {
    if (!this.article || this.busy || !this.browser) return;
    if (!window.confirm(this.locale.t('editor.deleteArticleConfirm', { title: this.article.title || this.article.slug }))) return;
    this.busy = true;
    this.deleting = true;
    this.error = '';
    this.editor?.setEditable(false, false);
    try {
      // Let an existing save finish, but do not save pending (possibly invalid) edits before deletion.
      if (this.savePromise) await this.savePromise;
      await firstValueFrom(this.content.deleteArticle(this.article.id, this.article.revision));
      this.savedSeq = this.changeSeq;
      this.article = null;
      await this.router.navigate(['/admin/publications']);
    } catch (error: any) { this.error = this.requestError(error, 'editor.deleteArticleFailed'); }
    finally {
      this.deleting = false;
      this.busy = false;
      if (this.article && this.editorReady) this.editor?.setEditable(true, false);
    }
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
    } catch (error: any) { this.error = this.requestError(error, 'editor.uploadFailed'); }
    finally { this.busy = false; }
  }

  async insertImage(asset: MediaAsset): Promise<void> {
    try {
      const url = await this.resolveMedia(asset.id);
      this.editor?.chain().focus().setImage({ src: url, alt: asset.fileName }).run();
      this.mediaOpen = false;
    } catch { this.error = 'editor.openImageFailed'; }
  }

  async deleteMedia(asset: MediaAsset): Promise<void> {
    if (this.busy || !this.editorReady) return;
    if (!window.confirm(this.locale.t('editor.deleteMediaConfirm', { name: asset.fileName }))) return;
    this.busy = true;
    this.error = '';
    try {
      // Persist pending insertions/removals before the server checks whether the image is in use.
      if (!await this.flush()) return;
      await firstValueFrom(this.content.deleteMedia(asset.id));
      this.media = this.media.filter(item => item.id !== asset.id);
      const url = this.blobById.get(asset.id);
      if (url) { URL.revokeObjectURL(url); this.idByBlob.delete(url); this.blobById.delete(asset.id); }
    } catch (error: any) { this.error = this.requestError(error, 'editor.deleteMediaFailed'); }
    finally { this.busy = false; }
  }

  removeCover(): void {
    if (!this.article || this.busy) return;
    this.article.coverId = null;
    this.coverPreview = '';
    this.changed();
  }

  async setCover(asset: MediaAsset): Promise<void> {
    if (!this.article) return;
    try {
      this.coverPreview = await this.resolveMedia(asset.id);
      this.article.coverId = asset.id;
      this.changed();
      this.mediaOpen = false;
    } catch { this.error = 'editor.openCoverFailed'; }
  }
}
