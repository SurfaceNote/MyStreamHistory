import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { PlaythroughSettingsComponent } from './playthrough-settings.component';
import { SettingsService } from '../../../../service/settings.service';
import { AnalyticsService } from '../../../../service/analytics.service';
import { Playthrough } from '../../../../models/playthrough.model';

describe('PlaythroughSettingsComponent analytics', () => {
  const playthrough: Playthrough = {
    id: 'p1', twitchCategoryId: 'c1', twitchCategoryTwitchId: '1', gameName: 'Game', boxArtUrl: '',
    title: '', status: 'Playing', autoAddNewStreams: false, createdAt: '', updatedAt: '', streamCategories: [],
  };
  let component: PlaythroughSettingsComponent;
  let analytics: jasmine.SpyObj<AnalyticsService>;

  beforeEach(() => {
    analytics = jasmine.createSpyObj<AnalyticsService>('AnalyticsService', ['trackEvent']);
    const settings = jasmine.createSpyObj<SettingsService>('SettingsService',
      ['getPlaythroughs', 'savePlaythrough', 'deletePlaythrough']);
    settings.getPlaythroughs.and.returnValue(of({ playthroughs: [playthrough], availableGames: [] }));
    settings.savePlaythrough.and.returnValue(of(playthrough));
    settings.deletePlaythrough.and.returnValue(of(undefined));
    TestBed.configureTestingModule({
      imports: [PlaythroughSettingsComponent],
      providers: [
        { provide: SettingsService, useValue: settings },
        { provide: AnalyticsService, useValue: analytics },
      ],
    });
    component = TestBed.createComponent(PlaythroughSettingsComponent).componentInstance;
  });

  it('tracks a playthrough created in settings, but not an edit', () => {
    component.form = { id: null, twitchCategoryId: 'c1', title: '', status: 'Planned', autoAddNewStreams: false, streamCategoryIds: [] };
    component.save();
    expect(analytics.trackEvent).toHaveBeenCalledOnceWith('playthrough_created', { source: 'settings', status: 'planned' });

    analytics.trackEvent.calls.reset();
    component.form = { id: 'p1', twitchCategoryId: 'c1', title: 'Renamed', status: 'Playing', autoAddNewStreams: false, streamCategoryIds: [] };
    component.save();
    expect(analytics.trackEvent).not.toHaveBeenCalled();
  });

  it('tracks a status change from the list', () => {
    component.changeStatus(playthrough, { value: 'Completed' } as HTMLSelectElement);
    expect(analytics.trackEvent).toHaveBeenCalledOnceWith('playthrough_status_changed', { status: 'completed', source: 'settings' });
  });

  it('tracks a deleted playthrough', () => {
    component.delete(playthrough);
    expect(analytics.trackEvent).toHaveBeenCalledOnceWith('playthrough_deleted', { source: 'settings', status: 'playing' });
  });
});
