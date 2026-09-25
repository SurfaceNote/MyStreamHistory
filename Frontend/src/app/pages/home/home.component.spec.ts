import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { HomeComponent } from './home.component';
import { AuthService } from '../../auth/auth.service';
import { StreamerService } from '../../service/streamer.service';

describe('Home streamer directory', () => {
  it('loads every streamer and refreshes live status', fakeAsync(() => {
    const streamers = Array.from({length: 15}, (_, i) => ({twitchId: i, displayName: `Streamer ${i}`, avatar: ''}));
    let live = true;
    const service = {getAllStreamers: () => of(streamers), getRecentStreams: jasmine.createSpy().and.callFake((id: number) => of([{isLive: id === 14 && live}]))};
    TestBed.configureTestingModule({providers: [
      {provide: AuthService, useValue: {isLoggedIn: () => false, getAccessTokenObservable: () => of(null)}},
      {provide: StreamerService, useValue: service}
    ]});
    const component = TestBed.runInInjectionContext(() => new HomeComponent());
    component.ngOnInit(); tick(0);
    expect(component.streamers.length).toBe(15);
    expect(component.liveStreamerIds.has(14)).toBeTrue();
    expect(component.liveStreamerIds.has(0)).toBeFalse();
    live = false; tick(60000);
    expect(component.liveStreamerIds.has(14)).toBeFalse();
    component.ngOnDestroy();
    const calls = service.getRecentStreams.calls.count(); tick(60000);
    expect(service.getRecentStreams.calls.count()).toBe(calls);
  }));
  it('shows recent streamers if the full directory API is not available', fakeAsync(() => {
    const streamers = [{twitchId: 1, displayName: 'Example', avatar: ''}];
    TestBed.configureTestingModule({providers: [
      {provide: AuthService, useValue: {isLoggedIn: () => false, getAccessTokenObservable: () => of(null)}},
      {provide: StreamerService, useValue: {
        getAllStreamers: () => throwError(() => new Error('Endpoint unavailable')),
        getStreamers: () => of(streamers), getRecentStreams: () => of([])
      }}
    ]});
    const component = TestBed.runInInjectionContext(() => new HomeComponent());
    component.ngOnInit(); tick(0);
    expect(component.streamers).toEqual(streamers);
    expect(component.showingRecentStreamers).toBeTrue();
    expect(component.streamersLoadFailed).toBeFalse();
    expect(component.isLoadingStreamers).toBeFalse();
    component.ngOnDestroy();
  }));

});
