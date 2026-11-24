import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ViewerStatsComponent } from './viewer-stats.component';
import { StreamerService } from '../../service/streamer.service';
import { SeoService } from '../../service/seo.service';
import { ViewerStats } from '../../models/viewer-stats.model';

describe('Viewer statistics', () => {
  it('links favorite categories to their game pages', () => {
    const stats: ViewerStats = {
      id: 'v', viewerId: 'v', streamerTwitchUserId: '1', minutesWatched: 120, earnedMsgPoints: 0, experience: 0,
      lastUpdatedAt: '2026-02-01T00:00:00Z', streamsWatched: 2, watchedPercentage: 50, recentStreams: [],
      favoriteCategories: [{ twitchCategoryId: '22197', name: 'colin mcrae: DiRT 2', boxArtUrl: '', minutesWatched: 90, streamsCount: 2 }],
      viewer: { id: 'v', twitchUserId: '5', displayName: 'Viewer', login: 'viewer', createdAt: '2026-01-01T00:00:00Z' }
    };
    TestBed.configureTestingModule({
      imports: [ViewerStatsComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ twitchId: '1', viewerTwitchId: '5' })) } },
        { provide: StreamerService, useValue: {
          getStreamerByTwitchId: () => of({ twitchId: 1, displayName: 'Streamer', avatar: '' }),
          getViewerStats: () => of(stats)
        } },
        { provide: SeoService, useValue: { update: () => {} } }
      ]
    });
    const fixture = TestBed.createComponent(ViewerStatsComponent);
    fixture.detectChanges();

    const link: HTMLAnchorElement = fixture.nativeElement.querySelector('.game-entry .game-card-link');
    expect(link.getAttribute('href')).toBe('/game/22197-colin-mcrae-dirt-2');
    expect(link.closest('.game-entry')!.classList).toContain('has-game-link');
  });
  function render(stats: Partial<ViewerStats>) {
    const full: ViewerStats = {
      id: 'v', viewerId: 'v', streamerTwitchUserId: '1', minutesWatched: 120, earnedMsgPoints: 0, experience: 0,
      lastUpdatedAt: '2026-02-01T00:00:00Z', streamsWatched: 2, watchedPercentage: 50, recentStreams: [], favoriteCategories: [],
      viewer: { id: 'v', twitchUserId: '5', displayName: 'Viewer', login: 'viewer', createdAt: '2026-01-01T00:00:00Z' },
      ...stats
    };
    TestBed.configureTestingModule({
      imports: [ViewerStatsComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ twitchId: '1', viewerTwitchId: '5' })) } },
        { provide: StreamerService, useValue: {
          getStreamerByTwitchId: () => of({ twitchId: 1, displayName: 'Streamer', avatar: '' }),
          getViewerStats: () => of(full)
        } },
        { provide: SeoService, useValue: { update: () => {} } }
      ]
    });
    const fixture = TestBed.createComponent(ViewerStatsComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the channel level, its badge and progress to the next level', () => {
    const page = render({
      experience: 32.805,
      level: { level: 2, currentLevelExperience: 7.29, nextLevelExperience: 58.32, progress: 0.5 }
    });

    const card = page.querySelector('.level-card')!;
    expect(card.querySelector('h2')!.textContent).toContain('Level 2');
    expect(card.querySelector('h2')!.textContent).toContain('Neophyte');
    expect(card.querySelector<HTMLImageElement>('app-viewer-level-badge img')!.getAttribute('src')).toBe('/images/levels/2.svg');
    expect(card.querySelector<HTMLElement>('.level-track span')!.style.width).toBe('50%');
    expect(card.querySelector('.level-meta')!.textContent).toContain('XP to the next level');
  });

  it('shows the maximum level without a next threshold', () => {
    const page = render({
      experience: 60000,
      level: { level: 20, currentLevelExperience: 50000, nextLevelExperience: null, progress: 1 }
    });

    const card = page.querySelector('.level-card')!;
    expect(card.querySelector('h2')!.textContent).toContain('Illuminate');
    expect(card.querySelector('.level-meta')!.textContent).toContain('Maximum level reached');
    expect(card.querySelectorAll('app-viewer-level-badge').length).toBe(1);
  });
});
