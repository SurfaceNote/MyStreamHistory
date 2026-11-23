import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of, throwError } from 'rxjs';
import { GamePageComponent } from './game-page.component';
import { gameIdFromSegment, gamePath, gameSegment, gameSourceFromState } from './game-path';
import { AnalyticsService } from '../../service/analytics.service';
import { GamePathPipe } from './game-path.pipe';
import { GameService } from '../../service/game.service';
import { SeoData, SeoService } from '../../service/seo.service';
import { ResponseStatusService } from '../../core/api/response-status.service';
import { GamePage, GameStreamer } from '../../models/game.model';

describe('Game path', () => {
  it('builds a readable segment and reads the id back', () => {
    expect(gameSegment('22197', 'colin mcrae: DiRT 2')).toBe('22197-colin-mcrae-dirt-2');
    expect(gamePath('1', 'Ведьмак 3: Дикая Охота')).toBe('/game/1-vedmak-3-dikaya-okhota');
    expect(gameSegment('7', '！！')).toBe('7');
    expect(gameIdFromSegment('22197-colin-mcrae-dirt-2')).toBe('22197');
    expect(gameIdFromSegment('22197')).toBe('22197');
    expect(gameIdFromSegment('dirt-2')).toBeNull();
    expect(gameIdFromSegment('22197abc')).toBeNull();
    expect(gameIdFromSegment(null)).toBeNull();
  });

  it('links only real Twitch category ids', () => {
    TestBed.configureTestingModule({});
    const pipe = TestBed.runInInjectionContext(() => new GamePathPipe());
    expect(pipe.transform('22197', 'colin mcrae: DiRT 2')).toBe('/game/22197-colin-mcrae-dirt-2');
    expect(pipe.transform('community-demo-chat', 'Demo')).toBeNull();
    expect(pipe.transform('', 'Empty')).toBeNull();
  });

  it('reads the link source from navigation state and treats anything else as direct', () => {
    expect(gameSourceFromState({ gameSource: 'stream_page' })).toBe('stream_page');
    expect(gameSourceFromState({ gameSource: 'somewhere' })).toBe('direct');
    expect(gameSourceFromState({ navigationId: 1 })).toBe('direct');
    expect(gameSourceFromState(undefined)).toBe('direct');
  });
});

describe('Game page', () => {
  const streamer = (id: number, overrides: Partial<GameStreamer> = {}): GameStreamer => ({
    twitchUserId: id, streamerLogin: 'streamer' + id, streamerDisplayName: 'Streamer' + id, streamerAvatarUrl: null,
    language: 'ru', hours: 1, streamsCount: 1, firstStreamedAt: '2026-01-01T00:00:00Z',
    lastStreamedAt: '2026-02-01T00:00:00Z', isLiveWithGame: false, playthrough: null, ...overrides
  });
  const page = (streamers: GameStreamer[], summary: Partial<GamePage['summary']> = {}): GamePage => ({
    summary: {
      twitchId: '22197', name: 'colin mcrae: DiRT 2', boxArtUrl: 'https://box/{width}x{height}.jpg', igdbId: '1', isGame: true,
      totalHours: 9.8, hoursLast30Days: 0, streamsCount: 15, channelsCount: streamers.length, languages: ['ru'],
      liveCount: streamers.filter(s => s.isLiveWithGame).length, completedCount: 1,
      firstStreamedAt: '2026-02-07T00:00:00Z', lastStreamedAt: '2026-02-20T00:00:00Z', ...summary
    },
    streamers
  });

  let seo: SeoData[];
  let requests: string[];
  let navigations: string[];
  let events: [string, Record<string, unknown>][];
  let params: BehaviorSubject<ReturnType<typeof convertToParamMap>>;

  function create(segment: string, response: () => any, navigationState?: Record<string, unknown>) {
    seo = []; requests = []; navigations = []; events = [];
    params = new BehaviorSubject(convertToParamMap({ gameId: segment }));
    TestBed.configureTestingModule({
      imports: [GamePageComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { paramMap: params } },
        { provide: GameService, useValue: { getGamePage: (id: string) => { requests.push(id); return response(); } } },
        { provide: SeoService, useValue: { update: (data: SeoData) => seo.push(data) } },
        { provide: ResponseStatusService, useValue: { reset: () => {}, fail: () => {} } },
        { provide: AnalyticsService, useValue: { trackEvent: (name: string, data: Record<string, unknown>) => events.push([name, data]) } }
      ]
    });
    const router = TestBed.inject(Router);
    // As in the app: the page initializes after the navigation that opened it has ended.
    spyOn(router, 'getCurrentNavigation').and.returnValue(null);
    spyOnProperty(router, 'lastSuccessfulNavigation').and.returnValue(navigationState ? { extras: { state: navigationState } } as any : null);
    spyOn(router, 'navigateByUrl').and.callFake((url: any) => { navigations.push(String(url)); return Promise.resolve(true); });
    const fixture = TestBed.createComponent(GamePageComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('shows the summary, the streamers and indexable SEO', () => {
    const game = page([
      streamer(1, { streamerDisplayName: 'kination', hours: 8.1, playthrough: { status: 'Completed', title: 'Run', streamsCount: 4 } }),
      streamer(2, { streamerDisplayName: 'SibirskyMraz', hours: 1.8, lastStreamedAt: '2026-01-15T00:00:00Z' })
    ]);
    const fixture = create('22197-colin-mcrae-dirt-2', () => of(game));
    const element: HTMLElement = fixture.nativeElement;

    expect(requests).toEqual(['22197']);
    expect(navigations).toEqual([]);
    expect(element.querySelector('h1')!.textContent).toContain('colin mcrae: DiRT 2');
    expect([...element.querySelectorAll('a.row .name')].map(n => n.textContent)).toEqual(['kination', 'SibirskyMraz']);
    expect(element.querySelector('.status[data-status="Completed"]')).not.toBeNull();
    expect(seo.at(-1)!.noIndex).toBeFalse();
    expect(seo.at(-1)!.image).toBe('https://box/288x384.jpg');
  });

  it('moves a short or outdated URL to the canonical one without loading twice', async () => {
    const fixture = create('22197', () => of(page([streamer(1), streamer(2)])));
    await fixture.whenStable();

    expect(navigations).toEqual(['/game/22197-colin-mcrae-dirt-2']);
    params.next(convertToParamMap({ gameId: '22197-colin-mcrae-dirt-2' }));
    expect(requests).toEqual(['22197']);
    expect(seo.length).toBeGreaterThan(0);
  });

  it('filters by language and sorts by hours', () => {
    const fixture = create('22197-colin-mcrae-dirt-2', () => of(page([
      streamer(1, { language: 'ru', hours: 1, lastStreamedAt: '2026-02-03T00:00:00Z' }),
      streamer(2, { language: 'cs', hours: 5, lastStreamedAt: '2026-02-02T00:00:00Z' }),
      streamer(3, { language: 'ru', hours: 3, lastStreamedAt: '2026-02-01T00:00:00Z' }),
      streamer(4, { language: null, hours: 2, lastStreamedAt: '2026-01-01T00:00:00Z' })
    ])));
    const component = fixture.componentInstance;

    expect(component.languageOptions[0]).toEqual(jasmine.objectContaining({ code: 'ru', count: 2 }));
    expect(component.languageOptions.map(o => o.code).sort()).toEqual(['', 'cs', 'ru']);
    component.selectLanguage('ru');
    expect(component.visibleStreamers.map(s => s.twitchUserId)).toEqual([1, 3]);
    component.selectLanguage('');
    expect(component.visibleStreamers.map(s => s.twitchUserId)).toEqual([4]);
    component.selectLanguage(null);
    component.selectSort('hours');
    expect(component.visibleStreamers.map(s => s.twitchUserId)).toEqual([2, 3, 4, 1]);
  });

  it('puts live streamers first and shows them in the live block', () => {
    const fixture = create('22197-colin-mcrae-dirt-2', () => of(page([
      streamer(1, { lastStreamedAt: '2026-02-03T00:00:00Z' }),
      streamer(2, { isLiveWithGame: true, lastStreamedAt: '2026-01-01T00:00:00Z' })
    ])));
    const component = fixture.componentInstance;

    expect(component.visibleStreamers.map(s => s.twitchUserId)).toEqual([2, 1]);
    expect(fixture.nativeElement.querySelectorAll('.live-card').length).toBe(1);
    component.selectSort('hours');
    // Equal hours: live still goes first.
    expect(component.visibleStreamers[0].twitchUserId).toBe(2);
  });

  it('hides the playthrough column when nobody tracks a playthrough', () => {
    const fixture = create('22197-colin-mcrae-dirt-2', () => of(page([streamer(1), streamer(2)])));

    expect(fixture.componentInstance.hasPlaythroughs).toBeFalse();
    expect(fixture.nativeElement.querySelector('.streamers.no-playthroughs')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.status')).toBeNull();
  });

  it('keeps thin pages and non-game categories out of search', () => {
    create('22197-colin-mcrae-dirt-2', () => of(page([streamer(1)])));
    expect(seo.at(-1)!.noIndex).toBeTrue();
    TestBed.resetTestingModule();

    const fixture = create('509658-just-chatting', () => of(page([streamer(1), streamer(2)], { twitchId: '509658', name: 'Just Chatting', isGame: false })));
    expect(seo.at(-1)!.noIndex).toBeTrue();
    expect(fixture.nativeElement.querySelector('.game-note')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.stat-completed')).toBeNull();
  });

  it('shows not found for an unknown game and for a segment without an id', () => {
    const fixture = create('999', () => throwError(() => ({ status: 404 })));
    expect(fixture.componentInstance.error).toBe('notFound');
    expect(seo.at(-1)!.noIndex).toBeTrue();
    TestBed.resetTestingModule();

    const bad = create('hollow-knight', () => of(page([])));
    expect(requests).toEqual([]);
    expect(bad.componentInstance.error).toBe('notFound');
  });

  it('reports a page open once, with the link it came from', async () => {
    const fixture = create('22197', () => of(page([streamer(1)])), { gameSource: 'profile_playthroughs' });
    await fixture.whenStable();
    params.next(convertToParamMap({ gameId: '22197-colin-mcrae-dirt-2' }));

    expect(events).toEqual([['game_page_open', { source: 'profile_playthroughs', is_game: true, streamers: 1 }]]);
    TestBed.resetTestingModule();

    create('509658', () => of(page([], { twitchId: '509658', name: 'Just Chatting', isGame: false })));
    expect(events).toEqual([['game_page_open', { source: 'direct', is_game: false, streamers: 0 }]]);
    TestBed.resetTestingModule();

    create('999', () => throwError(() => ({ status: 404 })));
    expect(events).toEqual([]);
  });

  it('reports a failure that is not a 404 as unavailable', () => {
    const fixture = create('22197', () => throwError(() => ({ status: 502 })));
    expect(fixture.componentInstance.error).toBe('failed');
  });
});
