import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ChangeDetectorRef, PLATFORM_ID } from '@angular/core';
import { provideRouter } from '@angular/router';
import { BehaviorSubject, Subject, of, throwError } from 'rxjs';
import { HomeComponent } from './home.component';
import { AuthService } from '../../auth/auth.service';
import { StreamerService } from '../../service/streamer.service';

describe('Home streamer directory', () => {
  it('loads every streamer and refreshes live status', fakeAsync(() => {
    const streamers = Array.from({length: 15}, (_, i) => ({twitchId: i, displayName: `Streamer ${i}`, avatar: ''}));
    let live = true;
    const service = {
      getAllStreamers: jasmine.createSpy().and.callFake(() => of(streamers.map(streamer => ({
        ...streamer, isLive: streamer.twitchId === 14 && live
      })))),
      getRecentStreams: jasmine.createSpy()
    };
    TestBed.configureTestingModule({providers: [
      {provide: ChangeDetectorRef, useValue: {markForCheck: () => {}, detectChanges: () => {}}},
      {provide: AuthService, useValue: {isLoggedIn: () => false, getAccessTokenObservable: () => of(null)}},
      {provide: StreamerService, useValue: service}
    ]});
    const component = TestBed.runInInjectionContext(() => new HomeComponent());
    component.ngOnInit(); tick(0);
    expect(component.streamers.length).toBe(15);
    expect(component.liveStreamerIds.has(14)).toBeTrue();
    expect(component.liveStreamerIds.has(0)).toBeFalse();
    expect(service.getAllStreamers.calls.count()).toBe(1);
    live = false; tick(60000);
    expect(component.liveStreamerIds.has(14)).toBeFalse();
    expect(service.getAllStreamers.calls.count()).toBe(2);
    expect(service.getRecentStreams).not.toHaveBeenCalled();
    component.ngOnDestroy();
    tick(60000);
    expect(service.getAllStreamers.calls.count()).toBe(2);
  }));

  it('preserves live status on a refresh failure and retries next minute', fakeAsync(() => {
    const streamers = [{twitchId: 1, displayName: 'Example', avatar: '', isLive: true}];
    const getAllStreamers = jasmine.createSpy().and.returnValues(
      of(streamers), throwError(() => new Error('Temporary failure')),
      of([{...streamers[0], isLive: false}, {twitchId: 2, displayName: 'New', avatar: '', isLive: true}])
    );
    TestBed.configureTestingModule({providers: [
      {provide: ChangeDetectorRef, useValue: {markForCheck: () => {}, detectChanges: () => {}}},
      {provide: AuthService, useValue: {getAccessTokenObservable: () => of(null)}},
      {provide: StreamerService, useValue: {getAllStreamers}}
    ]});
    const component = TestBed.runInInjectionContext(() => new HomeComponent());
    component.ngOnInit();
    tick(60000);
    expect(component.streamers).toEqual(streamers);
    expect(component.liveStreamerIds.has(1)).toBeTrue();
    tick(60000);
    expect(component.liveStreamerIds.has(1)).toBeFalse();
    expect(component.liveStreamerIds.has(2)).toBeTrue();
    expect(component.streamers.length).toBe(2);
    expect(getAllStreamers.calls.count()).toBe(3);
    component.ngOnDestroy();
  }));

  it('does not overlap slow refreshes and cancels them on destroy', fakeAsync(() => {
    const refresh = new Subject<any[]>();
    const getAllStreamers = jasmine.createSpy().and.returnValues(of([]), refresh);
    TestBed.configureTestingModule({providers: [
      {provide: ChangeDetectorRef, useValue: {markForCheck: () => {}, detectChanges: () => {}}},
      {provide: AuthService, useValue: {getAccessTokenObservable: () => of(null)}},
      {provide: StreamerService, useValue: {getAllStreamers}}
    ]});
    const component = TestBed.runInInjectionContext(() => new HomeComponent());
    component.ngOnInit();
    tick(120000);
    expect(getAllStreamers.calls.count()).toBe(2);
    component.ngOnDestroy();
    expect(refresh.observed).toBeFalse();
  }));
  it('shows recent streamers if the full directory API is not available', fakeAsync(() => {
    const streamers = [{twitchId: 1, displayName: 'Example', avatar: ''}];
    TestBed.configureTestingModule({providers: [
      {provide: ChangeDetectorRef, useValue: {markForCheck: () => {}, detectChanges: () => {}}},
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

describe('Home authentication after the first render', () => {
  for (const token of ['stored-session', null]) {
    it(`shows only a placeholder until auth resolves to ${token ? 'profile' : 'login'}`, fakeAsync(() => {
      const tokens = new Subject<string | null>();
      TestBed.configureTestingModule({
        imports: [HomeComponent],
        providers: [
          provideRouter([]),
          {provide: AuthService, useValue: {
            getAccessTokenObservable: () => tokens.asObservable(),
            getUsernameFromToken: () => 'kination',
            getTwitchIdFromToken: () => '106010980'
          }},
          {provide: StreamerService, useValue: {getAllStreamers: () => of([]), getRecentStreams: () => of([])}}
        ]
      });
      const fixture = TestBed.createComponent(HomeComponent);
      fixture.detectChanges();
      tick(0);
      fixture.detectChanges();
      const element: HTMLElement = fixture.nativeElement;
      expect(element.querySelectorAll('.auth-placeholder').length).toBe(2);
      expect(element.querySelectorAll('app-login-component').length).toBe(0);
      expect(element.querySelectorAll('a[href="/profile/106010980"]').length).toBe(0);
      tokens.next(token);
      fixture.detectChanges();
      expect(element.querySelectorAll('.auth-placeholder').length).toBe(0);
      expect(element.querySelectorAll('app-login-component').length).toBe(token ? 0 : 2);
      expect(element.querySelectorAll('a[href="/profile/106010980"]').length).toBe(token ? 2 : 0);
      fixture.destroy();
    }));
  }

  it('keeps the initial server view in the guest state without reading browser authentication', () => {
    const auth = {
      isLoggedIn: jasmine.createSpy().and.returnValue(true),
      getAccessTokenObservable: jasmine.createSpy().and.returnValue(of('stored-session')),
      getUsernameFromToken: () => 'kination',
      getTwitchIdFromToken: () => '106010980'
    };
    TestBed.configureTestingModule({providers: [
      {provide: PLATFORM_ID, useValue: 'server'},
      {provide: ChangeDetectorRef, useValue: {markForCheck: () => {}, detectChanges: () => {}}},
      {provide: AuthService, useValue: auth},
      {provide: StreamerService, useValue: {getAllStreamers: () => of([])}}
    ]});

    const component = TestBed.runInInjectionContext(() => new HomeComponent());
    component.ngOnInit();
    expect(component.isLoggedIn).toBeFalse();
    expect(component.authReady).toBeFalse();
    expect(auth.isLoggedIn).not.toHaveBeenCalled();
    expect(auth.getAccessTokenObservable).not.toHaveBeenCalled();
    component.ngOnDestroy();
  });

  it('replaces both login prompts with profile links for a stored session and restores them on logout', fakeAsync(() => {
    const tokens = new BehaviorSubject<string | null>('stored-session');
    TestBed.configureTestingModule({
      imports: [HomeComponent],
      providers: [
        provideRouter([]),
        {provide: AuthService, useValue: {
          getAccessTokenObservable: () => tokens.asObservable(),
          getUsernameFromToken: () => 'kination',
          getTwitchIdFromToken: () => '106010980'
        }},
        {provide: StreamerService, useValue: {
          getAllStreamers: () => of([]),
          getRecentStreams: () => of([])
        }}
      ]
    });

    const fixture = TestBed.createComponent(HomeComponent);
    fixture.detectChanges();
    tick(0);
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    expect(element.querySelectorAll('app-login-component').length).toBe(0);
    expect(element.querySelectorAll('a[href="/profile/106010980"]').length).toBe(2);

    const profile = element.querySelector<HTMLElement>('.hero-buttons .btn-primary')!;
    const discord = element.querySelector<HTMLElement>('.hero-buttons .btn-secondary')!;
    expect(discord.getBoundingClientRect().left - profile.getBoundingClientRect().right).toBeCloseTo(16, 0);
    expect(getComputedStyle(element.querySelector('.hero-text')!).animationName).toBe('none');

    tokens.next(null);
    fixture.detectChanges();
    expect(element.querySelectorAll('app-login-component').length).toBe(2);
    expect(element.querySelectorAll('a[href="/profile/106010980"]').length).toBe(0);
    expect(fixture.componentInstance.twitchId).toBe('');
    fixture.destroy();
  }));
});
