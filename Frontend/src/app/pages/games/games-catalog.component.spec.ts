import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of, throwError } from 'rxjs';
import { GAMES_PAGE_SIZE, GamesCatalogComponent, catalogQueryParams, catalogStateFromParams } from './games-catalog.component';
import { GameService } from '../../service/game.service';
import { SeoData, SeoService } from '../../service/seo.service';
import { ResponseStatusService } from '../../core/api/response-status.service';
import { GameSummary, GamesCatalog, GamesCatalogQuery } from '../../models/game.model';
import { API_ENDPOINTS } from '../../constants/api-endpoints';
import { AnalyticsService } from '../../service/analytics.service';

describe('Games catalog URL state', () => {
  it('reads known values and falls back to defaults for the rest', () => {
    expect(catalogStateFromParams(convertToParamMap({ q: '  hollow ', lang: 'RU', filter: 'live', sort: 'hours' })))
      .toEqual({ search: 'hollow', lang: 'ru', filter: 'live', sort: 'hours' });
    expect(catalogStateFromParams(convertToParamMap({ lang: 'russian', filter: 'dropped', sort: 'name' })))
      .toEqual({ search: '', lang: null, filter: 'all', sort: 'popular' });
  });

  it('leaves defaults out of the URL and the API request', () => {
    expect(catalogQueryParams({ search: '', lang: null, filter: 'all', sort: 'popular' }))
      .toEqual({ q: null, lang: null, filter: null, sort: null });
    expect(API_ENDPOINTS.GAMES({ filter: 'all', sort: 'popular', page: 1 })).toMatch(/\/games$/);
    expect(API_ENDPOINTS.GAMES({ search: 'dirt 2', lang: 'cs', filter: 'completed', sort: 'recent', page: 2, pageSize: 48 }))
      .toMatch(/\/games\?search=dirt\+2&lang=cs&filter=completed&sort=recent&page=2&pageSize=48$/);
  });
});

describe('Games catalog', () => {
  const game = (id: number, overrides: Partial<GameSummary> = {}): GameSummary => ({
    twitchId: String(id), name: 'Game ' + id, boxArtUrl: '', igdbId: String(id), isGame: true,
    totalHours: 10, hoursLast30Days: 1, streamsCount: 3, channelsCount: 2, languages: ['ru'],
    liveCount: 0, completedCount: 0, firstStreamedAt: '2026-01-01T00:00:00Z', lastStreamedAt: '2026-02-01T00:00:00Z', ...overrides
  });
  const response = (items: GameSummary[], page = 1, totalCount = items.length): GamesCatalog => ({
    page, pageSize: GAMES_PAGE_SIZE, totalCount, catalogCount: 29,
    languages: [{ language: 'ru', gamesCount: 15 }, { language: 'cs', gamesCount: 5 }], items
  });

  let requests: GamesCatalogQuery[];
  let seo: SeoData[];
  let navigations: any[];
  let events: [string, Record<string, unknown>][];
  let params: BehaviorSubject<ReturnType<typeof convertToParamMap>>;

  function create(query: Record<string, string>, answer: (q: GamesCatalogQuery) => any) {
    requests = []; seo = []; navigations = []; events = [];
    params = new BehaviorSubject(convertToParamMap(query));
    TestBed.configureTestingModule({
      imports: [GamesCatalogComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { queryParamMap: params } },
        { provide: GameService, useValue: { getCatalog: (q: GamesCatalogQuery) => { requests.push(q); return answer(q); } } },
        { provide: SeoService, useValue: { update: (data: SeoData) => seo.push(data) } },
        { provide: ResponseStatusService, useValue: { reset: () => {}, fail: () => {} } },
        { provide: AnalyticsService, useValue: { trackEvent: (name: string, data: Record<string, unknown>) => events.push([name, data]) } }
      ]
    });
    spyOn(TestBed.inject(Router), 'navigate').and.callFake((commands: any, extras: any) => {
      navigations.push(extras.queryParams);
      return Promise.resolve(true);
    });
    const fixture = TestBed.createComponent(GamesCatalogComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('loads the catalog from the URL and links each card to its game page', () => {
    const fixture = create({ lang: 'cs', filter: 'completed' }, () => of(response([
      game(22197, { name: 'colin mcrae: DiRT 2', completedCount: 1, liveCount: 2, languages: ['cs'] })
    ])));
    const element: HTMLElement = fixture.nativeElement;

    expect(requests[0]).toEqual(jasmine.objectContaining({ lang: 'cs', filter: 'completed', sort: 'popular', page: 1 }));
    expect(element.querySelector('a.card')!.getAttribute('href')).toBe('/game/22197-colin-mcrae-dirt-2');
    expect(element.querySelector('.badge.live')!.textContent).toContain('2');
    expect(element.querySelector('.badge.done')!.textContent).toContain('1');
    expect(element.querySelector('.chip.on[aria-pressed="true"]')).not.toBeNull();
    expect(seo.at(-1)!.noIndex).toBeFalsy();
  });

  it('turns filter clicks into URL changes and reloads when the URL changes', () => {
    const fixture = create({}, () => of(response([game(1)])));
    const component = fixture.componentInstance;

    component.setFilter('live');
    component.setLanguage('ru');
    component.setSort('hours');
    expect(navigations).toEqual([
      { q: null, lang: null, filter: 'live', sort: null },
      { q: null, lang: 'ru', filter: null, sort: null },
      { q: null, lang: null, filter: null, sort: 'hours' }
    ]);

    params.next(convertToParamMap({ filter: 'live' }));
    expect(requests.length).toBe(2);
    expect(requests[1].filter).toBe('live');
  });

  it('reports each changed control without the search text, and skips no-op clicks', fakeAsync(() => {
    const fixture = create({ filter: 'live' }, () => of(response([game(1)])));
    const component = fixture.componentInstance;

    component.setFilter('live');
    component.setFilter('completed');
    component.setLanguage('cs');
    component.setSort('recent');
    component.onSearch('hollow');
    tick(300);
    component.resetFilters();
    tick(300);

    expect(events.every(([name]) => name === 'games_catalog_filter')).toBeTrue();
    expect(events.map(([, data]) => data)).toEqual([
      { control: 'filter', option: 'completed' },
      { control: 'language', option: 'cs' },
      { control: 'sort', option: 'recent' },
      { control: 'search', option: 'text' },
      { control: 'reset', option: 'all' }
    ]);
  }));

  it('waits for a pause in typing before searching', fakeAsync(() => {
    const fixture = create({}, () => of(response([game(1)])));
    const component = fixture.componentInstance;

    component.onSearch('h');
    component.onSearch('ho');
    tick(200);
    expect(navigations).toEqual([]);
    component.onSearch('hollow ');
    tick(300);
    expect(navigations).toEqual([{ q: 'hollow', lang: null, filter: null, sort: null }]);
  }));

  it('appends the next page on show more', () => {
    const first = Array.from({ length: GAMES_PAGE_SIZE }, (_, i) => game(i + 1));
    const fixture = create({}, q => of(q.page === 1 ? response(first, 1, 50) : response([game(49), game(50)], 2, 50)));
    const component = fixture.componentInstance;

    expect(component.hasMore).toBeTrue();
    component.loadMore();
    fixture.detectChanges();
    expect(requests[1].page).toBe(2);
    expect(component.items.length).toBe(50);
    expect(component.hasMore).toBeFalse();
    expect(fixture.nativeElement.querySelector('.more-button')).toBeNull();
  });

  it('offers to reset filters when nothing matches', () => {
    const fixture = create({ q: 'nothing', filter: 'live' }, () => of(response([], 1, 0)));
    const element: HTMLElement = fixture.nativeElement;

    expect(element.querySelector('.state p')).not.toBeNull();
    (element.querySelector('button.reset') as HTMLButtonElement).click();
    expect(navigations.at(-1)).toEqual({ q: null, lang: null, filter: null, sort: null });
  });

  it('shows an error when the catalog cannot be loaded', () => {
    const fixture = create({}, () => throwError(() => ({ status: 502 })));

    expect(fixture.componentInstance.failed).toBeTrue();
    expect(seo.at(-1)!.noIndex).toBeTrue();
    expect(fixture.nativeElement.querySelector('a.card')).toBeNull();
  });
});
