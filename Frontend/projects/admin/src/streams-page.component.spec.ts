import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { AdminStream, StreamsPageComponent } from './streams-page.component';

describe('Admin streams list', () => {
  let http: HttpTestingController;
  let params: BehaviorSubject<ReturnType<typeof convertToParamMap>>;
  let router: jasmine.SpyObj<Router>;
  beforeEach(() => {
    params = new BehaviorSubject(convertToParamMap({ page: '2' }));
    router = jasmine.createSpyObj('Router', ['navigate']);
    TestBed.configureTestingModule({ imports: [StreamsPageComponent], providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: ActivatedRoute, useValue: { queryParamMap: params } }, { provide: Router, useValue: router }] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  const stream = (id: string, startedAt: string): AdminStream => ({ id, startedAt, streamerLogin: 'streamer',
    streamerDisplayName: 'Streamer', streamerAvatarUrl: null, endedAt: null, isLive: false,
    durationHours: 2.5, categories: ['Game'], uniqueViewers: 42 });
  const request = (page: number) => http.expectOne(req => req.url === '/api/admin/streams' && req.params.get('page') === String(page));

  it('loads the URL page, groups by local start day and renders stream details', () => {
    const fixture = TestBed.createComponent(StreamsPageComponent);
    const items = [stream('a', '2026-11-08T10:00:00'), stream('b', '2026-11-08T09:00:00'), stream('c', '2026-11-07T10:00:00')];
    request(2).flush({ success: true, data: { items, page: 2, pageSize: 25, totalCount: 70 } });
    fixture.detectChanges();
    expect(fixture.componentInstance.groups.map(g => g.streams.length)).toEqual([2, 1]);
    expect(fixture.nativeElement.querySelectorAll('tbody tr').length).toBe(3);
    expect(fixture.nativeElement.textContent).toContain('2,5 ч');
    expect(fixture.nativeElement.textContent).toContain('42');
    expect(fixture.nativeElement.querySelector('.profile-link').href).toBe('https://mystreamhistory.com/ru/stream/a');
    fixture.componentInstance.goToPage(3);
    expect(router.navigate).toHaveBeenCalledWith([], jasmine.objectContaining({ queryParams: { page: 3 } }));
  });
  it('cancels stale page requests, retries failures and distinguishes unavailable counts from zero', () => {
    const fixture = TestBed.createComponent(StreamsPageComponent);
    const stale = request(2);
    params.next(convertToParamMap({ page: '3' }));
    expect(stale.cancelled).toBeTrue();
    request(3).flush({}, { status: 503, statusText: 'Unavailable' });
    expect(fixture.componentInstance.error).toBeTruthy();
    fixture.componentInstance.retry();
    request(3).flush({ success: true, data: { items: [], page: 1, pageSize: 25, totalCount: 0 } });
    fixture.detectChanges();
    expect(fixture.componentInstance.error).toBe('');
    expect(fixture.nativeElement.textContent).toContain('Стримов пока нет');
    expect(fixture.componentInstance.viewers(null)).toBe('Нет данных');
    expect(fixture.componentInstance.viewers(0)).toBe('0');
  });
});
