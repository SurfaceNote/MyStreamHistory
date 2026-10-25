import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter, convertToParamMap } from '@angular/router';
import { BehaviorSubject, of, Subject, throwError } from 'rxjs';
import { StreamerProfileComponent } from './streamer-profile.component';
import { StreamerService } from '../../service/streamer.service';
import { SeoService } from '../../service/seo.service';
import { AuthService } from '../../auth/auth.service';

type TestAnalyticsWindow = Window & { gtag?: jasmine.Spy };

describe('StreamerProfileComponent', () => {
  let component: StreamerProfileComponent;
  let fixture: ComponentFixture<StreamerProfileComponent>;
  let analyticsWindow: TestAnalyticsWindow;
  let originalGtag: TestAnalyticsWindow['gtag'];
  let gtag: jasmine.Spy;
  let routeParams: BehaviorSubject<ReturnType<typeof convertToParamMap>>;
  const statistics: any = {
    totalStreamsCount: 20,
    totalUniqueGamesCount: 8,
    totalStreamedHours: 90,
    categories: Array.from({ length: 8 }, (_, i) => ({
      twitchCategoryId: String(i),
      name: `Game ${i}`,
      boxArtUrl: '',
      totalHours: i + 1,
    })),
    playthroughs: [],
    dashboard: null,
  };
  const streams: any[] = Array.from({ length: 5 }, (_, i) => ({
    id: String(i),
    twitchUserId: 1,
    streamerLogin: 'example',
    startedAt: '2026-09-20T12:00:00Z',
    endedAt: '2026-09-20T14:00:00Z',
    isLive: i === 1,
    gameName: `Game ${i}`,
    categories: [],
  }));
  beforeEach(async () => {
    routeParams = new BehaviorSubject(convertToParamMap({ twitchId: '1' }));
    analyticsWindow = window as TestAnalyticsWindow;
    originalGtag = analyticsWindow.gtag;
    gtag = jasmine.createSpy('gtag');
    analyticsWindow.gtag = gtag;
    await TestBed.configureTestingModule({
      imports: [StreamerProfileComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { paramMap: routeParams.asObservable() },
        },
        { provide: SeoService, useValue: { update: () => {} } },
        { provide: AuthService, useValue: { getTwitchIdFromToken: () => null, isAdmin: () => false } },
        {
          provide: StreamerService,
          useValue: {
            getStreamerByTwitchId: () =>
              of({ twitchId: 1, displayName: 'Example', avatar: '' }),
            getRecentStreams: () => of(streams),
            getStreamDetails: () => of({ viewers: [] }),
            getTopViewers: () => of([]),
            getSocialLinksByTwitchId: () => of({ socialLinks: [] }),
            getStreamerStatistics: () => of(statistics),
          },
        },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(StreamerProfileComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });
  afterEach(() => {
    if (originalGtag === undefined) {
      delete analyticsWindow.gtag;
    } else {
      analyticsWindow.gtag = originalGtag;
    }
  });

  it('links to the current Twitch login when it differs from the display name', () => {
    component.streamerShortDTO = {
      twitchId: 1, login: 'new_login', displayName: 'Новое имя', avatar: '',
    };
    fixture.detectChanges();
    const link: HTMLAnchorElement = fixture.nativeElement.querySelector('.social-icon.twitch');
    expect(link.href).toBe('https://www.twitch.tv/new_login');
  });

  it('keeps the Twitch link working with a legacy profile response', () => {
    const link: HTMLAnchorElement = fixture.nativeElement.querySelector('.social-icon.twitch');
    expect(link.href).toBe('https://www.twitch.tv/Example');
  });

  it('tracks each tab button click without counting the default overview', () => {
    expect(gtag).not.toHaveBeenCalled();
    const buttons: HTMLButtonElement[] = Array.from(
      fixture.nativeElement.querySelectorAll('.section-nav button'),
    );
    for (const button of buttons) {
      button.click();
      fixture.detectChanges();
      expect(gtag.calls.mostRecent().args).toEqual([
        'event', 'profile_tab_select',
        { tab_name: button.textContent!.trim().toLowerCase() },
      ]);
      expect(component.activeSection).toBe(button.textContent!.trim());
    }
    expect(gtag.calls.count()).toBe(4);
  });

  it('tracks selecting a section through its overview shortcut', () => {
    const button = Array.from<HTMLButtonElement>(
      fixture.nativeElement.querySelectorAll('button.text-button'),
    ).find(button => button.textContent?.includes('View all games'))!;
    button.click();
    fixture.detectChanges();
    expect(component.activeSection).toBe('Games');
    expect(gtag).toHaveBeenCalledOnceWith('event', 'profile_tab_select', {
      tab_name: 'games',
    });
  });

  it('allows tab navigation when analytics is unavailable', () => {
    delete analyticsWindow.gtag;
    const button: HTMLButtonElement = fixture.nativeElement.querySelectorAll('.section-nav button')[1];
    button.click();
    fixture.detectChanges();
    expect(component.activeSection).toBe('Streams');
    expect(gtag).not.toHaveBeenCalled();
  });
  it('prioritizes a live stream and falls back to the latest stream when offline', () => {
    expect(component.featuredStream?.id).toBe('1');
    component.recentStreams = streams.map((stream) => ({
      ...stream,
      isLive: false,
    }));
    expect(component.featuredStream?.id).toBe('0');
    component.recentStreams = [];
    expect(component.featuredStream).toBeUndefined();
  });

  it('clears the previous featured stream on a channel change and shows the new one before viewer details finish', () => {
    const service = TestBed.inject(StreamerService);
    const ownStreams = new Subject<any[]>();
    const ownDetails = new Subject<any>();
    spyOn(service, 'getRecentStreams').and.returnValue(ownStreams);
    spyOn(service, 'getStreamDetails').and.returnValue(ownDetails);
    expect(component.featuredStream?.id).toBe('1');

    routeParams.next(convertToParamMap({ twitchId: '2' }));
    fixture.detectChanges();
    expect(service.getRecentStreams).toHaveBeenCalledWith(2, 10);
    expect(component.featuredStream).toBeUndefined();
    expect(component.isLoadingStreams).toBeTrue();
    expect(fixture.nativeElement.querySelector('.featured-stream').textContent).toContain('Loading recent activity');

    ownStreams.next([{ ...streams[0], id: 'own', twitchUserId: 2 }]);
    ownStreams.complete();
    fixture.detectChanges();
    expect(component.featuredStream?.id).toBe('own');
    expect(component.isLoadingStreams).toBeFalse();
    expect(fixture.nativeElement.querySelectorAll('.stream-card').length).toBe(1);

    ownDetails.next({ viewers: [{}, {}] });
    ownDetails.complete();
    expect(component.featuredStream?.id).toBe('own');
    expect(component.featuredStream?.uniqueViewersCount).toBe(2);
  });

  it('cancels old list and viewer requests so they cannot overwrite the current channel', () => {
    const service = TestBed.inject(StreamerService);
    const oldList = new Subject<any[]>();
    const oldDetails = new Subject<any>();
    const ownList = new Subject<any[]>();
    spyOn(service, 'getRecentStreams').and.callFake((id) => id === 2 ? ownList : oldList);
    spyOn(service, 'getStreamDetails').and.returnValue(oldDetails);
    component.loadRecentStreams();
    oldList.next([{ ...streams[0], id: 'old-pending' }]);
    expect(component.featuredStream?.id).toBe('old-pending');
    expect(oldDetails.observed).toBeTrue();

    routeParams.next(convertToParamMap({ twitchId: '2' }));
    expect(oldList.observed).toBeFalse();
    expect(oldDetails.observed).toBeFalse();
    expect(component.isLoadingStreams).toBeTrue();
    (service.getStreamDetails as jasmine.Spy).and.returnValue(of({ viewers: [] }));
    ownList.next([{ ...streams[0], id: 'own', twitchUserId: 2 }]);
    ownList.complete();
    oldList.next(streams);
    oldDetails.next({ viewers: [] });
    oldDetails.complete();
    expect(component.featuredStream?.id).toBe('own');
    expect(component.recentStreams.length).toBe(1);
    expect(component.isLoadingStreams).toBeFalse();
  });

  for (const outcome of ['empty', 'error']) {
    it(`does not retain another channel's featured stream when the new list is ${outcome}`, () => {
      spyOn(console, 'error');
      spyOn(TestBed.inject(StreamerService), 'getRecentStreams').and.returnValue(
        outcome === 'empty' ? of([]) : throwError(() => new Error('Request failed')),
      );
      routeParams.next(convertToParamMap({ twitchId: '2' }));
      expect(component.featuredStream).toBeUndefined();
      expect(component.recentStreams).toEqual([]);
      expect(component.isLoadingStreams).toBeFalse();
    });
  }

  it('cancels pending stream loading when the profile is destroyed', () => {
    const pending = new Subject<any[]>();
    spyOn(TestBed.inject(StreamerService), 'getRecentStreams').and.returnValue(pending);
    component.loadRecentStreams();
    fixture.destroy();
    expect(pending.observed).toBeFalse();
  });
  it('limits the overview and expands the selected section', () => {
    expect(fixture.nativeElement.querySelectorAll('.stream-card').length).toBe(
      3,
    );
    expect(fixture.nativeElement.querySelectorAll('.summary-card').length).toBe(
      6,
    );
    component.selectSection('Streams');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.stream-card').length).toBe(
      5,
    );
    component.selectSection('Games');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.summary-card').length).toBe(
      8,
    );
  });
  it('filters and sorts games without changing the original data', () => {
    expect(component.filteredGames[0].name).toBe('Game 7');
    component.gameSort = 'name';
    expect(component.filteredGames[0].name).toBe('Game 0');
    component.gameQuery = '  GAME 3  ';
    expect(component.filteredGames.map((game) => game.name)).toEqual([
      'Game 3',
    ]);
    expect(statistics.categories[0].name).toBe('Game 0');
  });
  it('derives summary metrics from the selected period and distinguishes missing history from zero', () => {
    component.statistics = { ...statistics, dashboard: {
      totalStreamedHours: 15, totalStreamsCount: 3, totalUniqueGamesCount: 2,
      streamedHoursByDay: [{ date: '2026-10-01', value: 0 }, { date: '2026-10-02', value: 15 }],
      chatPointsByDay: [{ date: '2026-10-01', value: 12 }, { date: '2026-10-02', value: 3.5 }],
      topCategories: [],
    } };
    const values = () => Object.fromEntries(component.dashboardSummaryCards.map(card => [card.label, card.value]));
    expect(values()['Average stream']).toBe('5 h');
    expect(values()['Active days']).toBe('1');
    expect(values()['Chat points']).toBe('15.5');
    component.statistics!.dashboard.totalStreamsCount = 0;
    component.statistics!.dashboard.streamedHoursByDay = [];
    component.statistics!.dashboard.chatPointsByDay = [];
    expect(values()['Average stream']).toBe('—');
    expect(values()['Active days']).toBe('—');
    expect(values()['Chat points']).toBe('—');
  });

  it('limits broadcast activity to the latest 35 recorded days without mutating the source', () => {
    const series = Array.from({ length: 40 }, (_, i) => ({
      date: new Date(Date.UTC(2026, 8, i + 1)).toISOString().slice(0, 10), value: i,
    }));
    component.statistics = { ...statistics, dashboard: { streamedHoursByDay: series } };
    expect(component.activityDays.length).toBe(35);
    expect(component.activityDays[0]).toEqual({ date: '2026-09-06', label: '6', hours: 5 });
    expect(series.length).toBe(40);
    component.statistics = null;
    expect(component.activityDays).toEqual([]);
  });
  it('keeps empty playthrough sections out of the page', () => {
    component.selectSection('Games');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.playthrough-card')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain(
      'No playthroughs found',
    );
  });
  it('keeps every game cover in the scrollable strip without visible game names', () => {
    component.recentStreams = [
      {
        ...streams[0],
        streamTitle: 'Hidden stream title',
        categories: Array.from({ length: 25 }, (_, i) => ({
          twitchId: String(i),
          name: `Category ${i}`,
          boxArtUrl: '',
        })),
      },
    ];
    fixture.detectChanges();
    const card = fixture.nativeElement.querySelector('.stream-card');
    expect(card.querySelectorAll('.stream-game-item').length).toBe(25);
    expect(card.querySelector('.stream-game-list').getAttribute('tabindex')).toBe('0');
    expect(card.querySelector('.stream-game-item').getAttribute('aria-label')).toBe('Category 0');
    expect(card.textContent).not.toContain('Category 0');
    expect(card.querySelector('.more-games')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain(
      'Hidden stream title',
    );
    expect(
      fixture.nativeElement.querySelector('a[aria-label="Twitch"] i.fa-twitch'),
    ).not.toBeNull();
  });
  it('opens the stream once from the card background, cover, footer or date link', () => {
    const navigate = spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);
    const card: HTMLElement = fixture.nativeElement.querySelector('.stream-card');
    for (const target of [card, card.querySelector('.stream-game-item')!, card.querySelector('.stream-footer')!, card.querySelector('a')!]) {
      navigate.calls.reset();
      target.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
      expect(navigate).toHaveBeenCalledTimes(1);
      expect(navigate.calls.mostRecent().args[0].toString()).toBe('/stream/0');
    }
  });

  it('does not navigate after dragging covers, but allows the next normal click', () => {
    const navigate = spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);
    const cover: HTMLElement = fixture.nativeElement.querySelector('.stream-card .stream-game-item');
    cover.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, pointerId: 1, clientX: 100, clientY: 100 }));
    cover.dispatchEvent(new PointerEvent('pointermove', { bubbles: true, pointerId: 1, clientX: 50, clientY: 100 }));
    cover.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
    expect(navigate).not.toHaveBeenCalled();
    cover.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, pointerId: 1, clientX: 100, clientY: 100 }));
    cover.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
    expect(navigate).toHaveBeenCalledTimes(1);
  });
  it('shows current and completed playthroughs on the overview and filters game status', () => {
    component.statistics = { ...statistics, playthroughs: ['Playing', 'Completed', 'Planned', 'Dropped'].map((status, i) => ({ playthroughId: String(i), gameName: status + ' game', title: '', status, twitchCategoryId: String(i), twitchCategoryTwitchId: String(i), boxArtUrl: '', uniqueViewersCount: 0, totalHours: 3 })) };
    fixture.detectChanges();
    const overview = fixture.nativeElement.querySelectorAll('.overview-playthroughs');
    expect(overview.length).toBe(2);
    expect(overview[0].textContent).toContain('Playing game');
    expect(overview[1].textContent).toContain('Completed game');
    component.selectSection('Games'); component.gameStatus = 'Planned'; fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.playthrough-card').textContent).toContain('Planned game');
    expect(fixture.nativeElement.querySelectorAll('.playthrough-card').length).toBe(1);
  });

});
