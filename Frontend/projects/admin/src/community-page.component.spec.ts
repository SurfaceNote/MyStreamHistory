import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { BehaviorSubject, Subject } from 'rxjs';
import { buildRows, CommunityChannelSummary, CommunityPageComponent } from './community-page.component';

const flushAsync = () => new Promise(resolve => setTimeout(resolve));

describe('Admin community page', () => {
  let http: HttpTestingController;
  let query: BehaviorSubject<ReturnType<typeof convertToParamMap>>;
  let navigate: jasmine.Spy;
  const summary = (id: string, extra: Partial<CommunityChannelSummary> = {}): CommunityChannelSummary => ({
    twitchUserId: id, quality: 'verified', recalculating: false, totalBroadcasts: 24,
    segments: { core: 12, regular: 20, occasional: 9, new: 4, returned: 1, fading: 2, lost: 3, oneTime: 30 },
    asOf: '2026-11-07', calculatedAt: '2026-11-08T10:00:00Z', lastActivityAt: null, ...extra
  });
  beforeEach(() => {
    query = new BehaviorSubject(convertToParamMap({}));
    navigate = jasmine.createSpy('navigate').and.resolveTo(true);
    TestBed.configureTestingModule({ imports: [CommunityPageComponent], providers: [
      provideHttpClient(), provideHttpClientTesting(),
      { provide: ActivatedRoute, useValue: { queryParamMap: query } },
      { provide: Router, useValue: { navigate, events: new Subject(), url: '/community' } }
    ] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  function flush() {
    http.expectOne('/api/user/all').flush({ success: true, data: [
      { twitchId: 1, displayName: 'Small', login: 'small', avatar: '' },
      { twitchId: 2, displayName: 'Big', login: 'big', avatar: 'https://example.test/a.png' },
      { twitchId: 3, displayName: 'Silent', login: 'silent', avatar: '' }
    ] });
    http.expectOne('/api/analytics/community/overview').flush({ success: true, data: { channels: [
      summary('2'), summary('1', { quality: 'insufficient', totalBroadcasts: 3, segments: { new: 2, occasional: 1 } }), summary('9', { recalculating: true, totalBroadcasts: 0 })
    ] } });
  }

  it('lists every channel with its segments, the busiest first', async () => {
    const fixture = TestBed.createComponent(CommunityPageComponent);
    flush(); await flushAsync(); fixture.detectChanges();
    const rows: HTMLTableRowElement[] = Array.from(fixture.nativeElement.querySelectorAll('tbody tr'));
    expect(rows.map(r => r.querySelector('strong')!.textContent)).toEqual(['Big', 'Small', 'Канал 9', 'Silent']);
    expect(rows[0].textContent).toContain('12');
    expect(rows[0].textContent).toContain('Готов');
    // Core cannot be determined from three streams.
    expect(rows[1].querySelectorAll('td')[2].textContent!.trim()).toBe('—');
    expect(rows[1].textContent).toContain('Мало стримов');
    expect(rows[2].textContent).toContain('Пересчёт');
    expect(rows[3].textContent).toContain('Нет данных');
    expect(fixture.nativeElement.querySelector('app-community')).toBeNull();
  });

  it('filters channels and opens one by clicking its row', async () => {
    const fixture = TestBed.createComponent(CommunityPageComponent);
    flush(); await flushAsync(); fixture.detectChanges();
    const page = fixture.componentInstance;
    page.withDataOnly = true;
    expect(page.visibleRows.map(r => r.twitchId)).toEqual(['2', '1', '9']);
    page.query = 'SMA';
    expect(page.visibleRows.map(r => r.twitchId)).toEqual(['1']);
    page.query = ''; fixture.detectChanges();
    fixture.nativeElement.querySelector('tbody tr').click();
    expect(navigate).toHaveBeenCalledWith([], jasmine.objectContaining({ queryParams: { channelId: '2' } }));
  });

  it('shows one heading and one channel picker above the embedded report', async () => {
    query.next(convertToParamMap({ channelId: '2' }));
    const fixture = TestBed.createComponent(CommunityPageComponent);
    flush(); await flushAsync(); fixture.detectChanges();
    // The embedded report's own requests are covered by its spec.
    http.match(() => true).forEach(request => request.flush(null, { status: 503, statusText: 'Unavailable' }));
    expect(fixture.nativeElement.querySelectorAll('h1').length).toBe(1);
    expect(fixture.nativeElement.querySelector('h1').textContent).toBe('Big');
    expect(fixture.nativeElement.querySelectorAll('select').length).toBe(1);
    const embedded = fixture.nativeElement.querySelector('app-community');
    expect(embedded.classList).toContain('embedded');
    fixture.nativeElement.querySelector('.back').click();
    expect(navigate).toHaveBeenCalledWith([], jasmine.objectContaining({ queryParams: { channelId: null } }));
  });

  it('keeps channels that only exist in the community data', () => {
    const rows = buildRows([], [summary('77')]);
    expect(rows.map(r => [r.twitchId, r.name])).toEqual([['77', 'Канал 77']]);
  });
});
