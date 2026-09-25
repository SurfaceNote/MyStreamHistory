import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { StreamerProfileComponent } from './streamer-profile.component';
import { StreamerService } from '../../service/streamer.service';
import { SeoService } from '../../service/seo.service';

describe('StreamerProfileComponent', () => {
  let component: StreamerProfileComponent;
  let fixture: ComponentFixture<StreamerProfileComponent>;
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
    await TestBed.configureTestingModule({
      imports: [StreamerProfileComponent],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: { paramMap: of(convertToParamMap({ twitchId: '1' })) },
        },
        {
          provide: Router,
          useValue: { navigate: jasmine.createSpy('navigate') },
        },
        { provide: SeoService, useValue: { update: () => {} } },
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
  it('keeps empty playthrough sections out of the page', () => {
    component.selectSection('Games');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.playthrough-card')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain(
      'No playthroughs found',
    );
  });
  it('keeps many-game streams compact and omits stream titles', () => {
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
    expect(card.querySelectorAll('.stream-covers img').length).toBe(4);
    expect(card.querySelector('.more-games').textContent).toContain('+21');
    expect(card.textContent).toContain('25 games played');
    expect(fixture.nativeElement.textContent).not.toContain(
      'Hidden stream title',
    );
    expect(
      fixture.nativeElement.querySelector('a[aria-label="Twitch"] i.fa-twitch'),
    ).not.toBeNull();
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
