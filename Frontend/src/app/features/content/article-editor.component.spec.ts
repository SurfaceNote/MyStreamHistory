import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed, fakeAsync, flushMicrotasks, tick } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ArticleEditorComponent } from './article-editor.component';
import { Article } from './content.models';
import { CONTENT_API_BASE } from './content.service';

describe('Article editor writing and persistence', () => {
  let fixture: ComponentFixture<ArticleEditorComponent>;
  let http: HttpTestingController;
  let draft: Article;
  const endpoint = '/api/content/admin/articles/draft-id';

  beforeEach(() => {
    draft = {
      id: 'draft-id', type: 'news', slug: 'untitled-123', title: '', summary: '',
      seoTitle: '', seoDescription: '', coverId: null, revision: 1,
      slugLocked: false, publishedAt: null, updatedAt: '',
      body: { type: 'doc', content: [{ type: 'paragraph', content: [{ type: 'text', text: 'Existing draft text' }] }] }
    };
    TestBed.configureTestingModule({
      imports: [ArticleEditorComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: CONTENT_API_BASE, useValue: '/api' },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ id: draft.id })) } }]
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ArticleEditorComponent);
    fixture.detectChanges();
  });

  afterEach(() => { fixture.destroy(); http.verify(); });

  function load(): void {
    http.expectOne(endpoint).flush(structuredClone(draft));
    flushMicrotasks();
    http.expectOne('/api/content/admin/media').flush([]);
    fixture.detectChanges();
    flushMicrotasks();
    fixture.detectChanges();
    tick(0);
  }

  it('mounts the writing surface after the delayed draft response and restores its body', fakeAsync(() => {
    expect(fixture.componentInstance.editor).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Loading editor');
    load();
    expect(fixture.componentInstance.editorReady).toBeTrue();
    expect(fixture.nativeElement.querySelector('[contenteditable="true"]').textContent).toContain('Existing draft text');
    expect(fixture.componentInstance.saveState).toBe('Saved');
    http.expectNone(request => request.method === 'PUT');
  }));

  it('autosaves typed text and restores the saved document on reopening', fakeAsync(() => {
    load();
    fixture.componentInstance.editor!.commands.insertContent(' New paragraph');
    tick(1199);
    http.expectNone(request => request.method === 'PUT');
    tick(1);
    const save = http.expectOne(endpoint);
    expect(save.request.method).toBe('PUT');
    expect(JSON.stringify(save.request.body.body)).toContain('New paragraph');
    expect(save.request.body.revision).toBe(1);
    draft = { ...draft, ...save.request.body, revision: 2 };
    save.flush(draft);
    flushMicrotasks();
    expect(fixture.componentInstance.saveState).toBe('Saved');
    fixture.destroy();
    fixture = TestBed.createComponent(ArticleEditorComponent);
    fixture.detectChanges();
    load();
    expect(fixture.componentInstance.editor!.getText()).toContain('New paragraph');
  }));

  it('saves edits made during an in-flight save using the returned revision', fakeAsync(() => {
    load();
    const component = fixture.componentInstance;
    component.editor!.commands.insertContent(' First edit');
    let result: boolean | undefined;
    void component.flush().then(value => result = value);
    const first = http.expectOne(endpoint);
    component.editor!.commands.insertContent(' Second edit');
    first.flush({ ...draft, revision: 2 });
    flushMicrotasks();
    const second = http.expectOne(endpoint);
    expect(second.request.body.revision).toBe(2);
    expect(JSON.stringify(second.request.body.body)).toContain('Second edit');
    second.flush({ ...draft, revision: 3 });
    flushMicrotasks();
    expect(result).toBeTrue();
    expect(component.article!.revision).toBe(3);
    tick(1200);
    http.expectNone(request => request.method === 'PUT');
  }));

  it('keeps unsaved text after a failed save and allows retry before leaving', fakeAsync(() => {
    load();
    const component = fixture.componentInstance;
    component.editor!.commands.insertContent(' Keep my text');
    let result: boolean | undefined;
    void component.flush().then(value => result = value);
    http.expectOne(endpoint).flush('Article URL is already in use.', { status: 409, statusText: 'Conflict' });
    flushMicrotasks();
    expect(result).toBeFalse();
    expect(component.error).toBe('Article URL is already in use.');
    expect(component.editor!.getText()).toContain('Keep my text');
    const event = new Event('beforeunload', { cancelable: true }) as BeforeUnloadEvent;
    component.beforeUnload(event);
    expect(event.defaultPrevented).toBeTrue();
    void component.flush().then(value => result = value);
    const retry = http.expectOne(endpoint);
    expect(JSON.stringify(retry.request.body.body)).toContain('Keep my text');
    retry.flush({ ...draft, revision: 2 });
    flushMicrotasks();
    expect(result).toBeTrue();
    expect(component.error).toBe('');
    tick(1200);
  }));

  it('waits for the current draft save before opening preview', fakeAsync(() => {
    load();
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    fixture.componentInstance.editor!.commands.insertContent(' Preview this');
    void fixture.componentInstance.preview();
    expect(navigate).not.toHaveBeenCalled();
    http.expectOne(endpoint).flush({ ...draft, revision: 2 });
    flushMicrotasks();
    expect(navigate).toHaveBeenCalledWith(['/admin/publications', draft.id, 'preview']);
    tick(1200);
  }));

  it('preserves uploaded image references and never saves an empty body during hydration', fakeAsync(() => {
    const id = '11111111-1111-1111-1111-111111111111';
    draft.body.content!.push({ type: 'image', attrs: { src: `/content/media/${id}`, alt: 'Cover' } });
    http.expectOne(endpoint).flush(draft);
    flushMicrotasks();
    http.expectOne('/api/content/admin/media').flush([]);
    fixture.detectChanges();
    flushMicrotasks();
    expect(fixture.componentInstance.editorReady).toBeFalse();
    fixture.componentInstance.article!.title = 'Changed title';
    fixture.componentInstance.titleChanged();
    void fixture.componentInstance.flush();
    flushMicrotasks();
    http.expectNone(request => request.method === 'PUT');
    http.expectOne(`/api/content/media/${id}`).flush(new Blob(['image'], { type: 'image/png' }));
    flushMicrotasks();
    fixture.detectChanges();
    expect(fixture.componentInstance.editorReady).toBeTrue();
    void fixture.componentInstance.flush();
    const save = http.expectOne(endpoint);
    expect(JSON.stringify(save.request.body.body)).toContain(`/content/media/${id}`);
    expect(JSON.stringify(save.request.body.body)).not.toContain('blob:');
    expect(JSON.stringify(save.request.body.body)).toContain('Existing draft text');
    save.flush({ ...draft, revision: 2 });
    flushMicrotasks();
    tick(1200);
  }));

  it('saves incomplete drafts and explains the missing publication requirements', fakeAsync(() => {
    draft.body = { type: 'doc', content: [] };
    load();
    const component = fixture.componentInstance;
    void component.publish();
    flushMicrotasks();
    expect(component.error).toContain('headline, cover image, article text');
    http.expectNone(request => request.method === 'POST');
    component.article!.title = 'A draft headline';
    component.titleChanged();
    void component.flush();
    const save = http.expectOne(endpoint);
    expect(save.request.body.summary).toBe('');
    save.flush({ ...draft, revision: 2 });
    flushMicrotasks();
    expect(component.saveState).toBe('Saved');
    tick(1200);
  }));

  it('shows a usable error and a way back when a draft cannot be loaded', fakeAsync(() => {
    http.expectOne(endpoint).flush('Not found', { status: 404, statusText: 'Not Found' });
    flushMicrotasks();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('Could not load this draft');
    expect(fixture.nativeElement.querySelector('a').getAttribute('href')).toBe('/admin/publications');
  }));

  for (const type of ['news', 'review'] as const) {
    it(`publishes a ${type} article without a summary`, fakeAsync(() => {
      draft = { ...draft, type, title: 'Article title', coverId: '11111111-1111-1111-1111-111111111111' };
      http.expectOne(endpoint).flush(structuredClone(draft));
      flushMicrotasks();
      http.expectOne(`/api/content/media/${draft.coverId}`).flush(new Blob(['image'], { type: 'image/png' }));
      flushMicrotasks();
      http.expectOne('/api/content/admin/media').flush([]);
      fixture.detectChanges();
      flushMicrotasks();
      fixture.detectChanges();
      tick(0);
      expect(fixture.componentInstance.publicationRequirements).toEqual([]);
      void fixture.componentInstance.publish();
      flushMicrotasks();
      const publish = http.expectOne(`${endpoint}/publish`);
      expect(publish.request.method).toBe('POST');
      expect(publish.request.body).toBe(1);
      publish.flush({ ...draft, revision: 2, publishedAt: '2026-10-02T00:00:00Z' });
      flushMicrotasks();
      expect(fixture.componentInstance.saveState).toBe('Published');
      expect(fixture.componentInstance.article!.summary).toBe('');
    }));
  }
});
