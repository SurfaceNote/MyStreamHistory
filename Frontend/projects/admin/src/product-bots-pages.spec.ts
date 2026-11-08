import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ProductChannel, ProductPageComponent } from './product-page.component';
import { BotsPageComponent } from './bots-page.component';

const flushAsync = () => new Promise(resolve => setTimeout(resolve));

describe('Admin product metrics', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [ProductPageComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  const channel = (id: number, tracking: ProductChannel['tracking'], extra: Partial<ProductChannel> = {}): ProductChannel => ({
    twitchUserId: id, login: 'channel' + id, displayName: 'Channel ' + id, avatarUrl: null, registeredAt: null,
    streamsCount: 3, streamsLast28Days: 1, lastStreamStartedAt: '2026-11-03T18:00:00Z', isLive: false, tracking,
    lastVisitAt: null, lastOverlayAt: null, lastPanelAt: null, ...extra,
  });

  it('shows the weekly engaged channels, activation and channels with recording problems', async () => {
    const fixture = TestBed.createComponent(ProductPageComponent);
    http.expectOne(req => req.url === '/api/admin/product/overview' && req.params.get('weeks') === '12').flush({ success: true, data: {
      generatedAt: '2026-11-09T10:00:00Z', activityTrackedSince: '2026-11-02',
      weeks: [{ weekStart: '2026-10-26', channelsStreamed: 4, engagedChannels: 1 }, { weekStart: '2026-11-02', channelsStreamed: 5, engagedChannels: 3 }],
      activation: [{ weekStart: '2026-11-02', registered: 2, streamedWithin14Days: 1, activated: 1 }],
      channels: [channel(1, 'active', { lastVisitAt: '2026-11-08T12:00:00Z' }), channel(2, 'missing'), channel(3, 'paused', { login: 'other' })],
    } });
    await flushAsync();
    fixture.detectChanges();
    const page = fixture.componentInstance;
    const text: string = fixture.nativeElement.textContent;
    expect(page.current?.engagedChannels).toBe(3);
    expect(text).toContain('из 5 стримивших');
    expect(text).toContain('60%');
    expect(text).toContain('1 (50%)');
    expect(page.problems.map(c => c.twitchUserId)).toEqual([2, 3]);
    expect(fixture.nativeElement.querySelectorAll('[data-state="missing"]').length).toBe(1);

    page.problemsOnly = true;
    page.query = 'OTHER';
    expect(page.visibleChannels.map(c => c.twitchUserId)).toEqual([3]);
  });

  it('reports a failure and retries', async () => {
    const fixture = TestBed.createComponent(ProductPageComponent);
    http.expectOne('/api/admin/product/overview?weeks=12').flush(null, { status: 503, statusText: 'Unavailable' });
    await flushAsync();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('Не удалось загрузить метрики');
    fixture.nativeElement.querySelector('button').click();
    http.expectOne('/api/admin/product/overview?weeks=12').flush({ success: true, data: { generatedAt: '2026-11-09T10:00:00Z',
      activityTrackedSince: null, weeks: [], activation: [], channels: [] } });
    await flushAsync();
    expect(fixture.componentInstance.error).toBe('');
  });
});

describe('Admin bot list', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [BotsPageComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  const list = (accounts: string[]) => ({ success: true, data: {
    accounts: accounts.map(login => ({ login, displayName: null, addedBy: 'default', addedAt: '2026-11-09T00:00:00Z' })),
    candidates: [{ twitchUserId: '20', login: 'lurker', displayName: 'Lurker', channelsCount: 3, highPresenceChannels: 3,
      maxPresenceShare: 0.7, totalMinutes: 1860, messagePoints: 0,
      channels: [{ streamerTwitchUserId: '123', streamerDisplayName: 'kination', presenceShare: 0.7, minutes: 700 }] }],
  } });

  it('lists bots and candidates, adds a candidate and removes a bot', async () => {
    const fixture = TestBed.createComponent(BotsPageComponent);
    http.expectOne('/api/admin/bots').flush(list(['nightbot']));
    await flushAsync();
    fixture.detectChanges();
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('nightbot');
    expect(text).toContain('список по умолчанию');
    expect(text).toContain('kination · 70%');
    expect(text).toContain('31 ч');

    void fixture.componentInstance.add('Lurker');
    const post = http.expectOne(req => req.method === 'POST' && req.url === '/api/admin/bots');
    expect(post.request.body).toEqual({ login: 'lurker' });
    post.flush({ success: true, data: { error: null } });
    await flushAsync();
    http.expectOne('/api/admin/bots').flush(list(['lurker', 'nightbot']));
    await flushAsync();
    expect(fixture.componentInstance.message).toBe('lurker добавлен в список ботов.');

    void fixture.componentInstance.remove('nightbot');
    http.expectOne(req => req.method === 'DELETE' && req.url === '/api/admin/bots/nightbot').flush({ success: true, data: { error: null } });
    await flushAsync();
    http.expectOne('/api/admin/bots').flush(list(['lurker']));
    await flushAsync();
    expect(fixture.componentInstance.data?.accounts.map(a => a.login)).toEqual(['lurker']);
  });

  it('rejects invalid logins without calling the API', async () => {
    const fixture = TestBed.createComponent(BotsPageComponent);
    http.expectOne('/api/admin/bots').flush(list([]));
    await fixture.componentInstance.add('not a login!');
    expect(fixture.componentInstance.error).toContain('от 1 до 25');
    http.expectNone('/api/admin/bots');
  });
});
