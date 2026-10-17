import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { BehaviorSubject, of, Subject, throwError } from 'rxjs';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { CommunityComponent } from './community.component';
import { CommunityService } from './community.service';
import { CommunityMember, CommunityReport, CommunityResponse } from './community.models';
import { AnalyticsService } from '../../service/analytics.service';
import { AuthService } from '../../auth/auth.service';
import { StreamerService } from '../../service/streamer.service';

describe('Community analytics', () => {
  const browserZone = Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
  const report: CommunityReport = {
    methodVersion: 1, calculatedAt: '2026-10-17T08:00:00Z', asOf: '2026-10-16', from: '2026-09-19',
    windowDays: 28, timeZoneId: browserZone, timeZoneConfigured: true, quality: 'provisional', recalculating: false,
    collectionEnabled: true, eligibleDays: 12, unknownDays: 1, coverage: .98, core: 38, previousCore: 34,
    regularIncludingCore: 90, firstObserved: 17, declining: 6,
    distribution: { core: 38, regular: 52, returning: 41, occasional: 49 },
    trend: [{ date: '2026-10-15', core: 37, quality: 'provisional' }, { date: '2026-10-16', core: 38, quality: 'provisional' }], transitions: { enteredCore: 7 }
  };
  const member: CommunityMember = {
    twitchUserId: '1', displayName: 'Quiet', login: 'quiet', profileImageUrl: null, level: 'core',
    quality: 'provisional', visitedDays: 10, eligibleDays: 12, regularity: 10 / 12,
    messageCount: null, daysWithMessages: null, hasHistoricalChat: true, messagesProvisional: true,
    firstObserved: '2026-09-20', consecutiveMisses: 0, declining: false, dormant: false, transition: null,
    history: [
      { date: '2026-10-12', status: 'visited', quality: 'provisional' },
      { date: '2026-10-14', status: 'unknown', quality: 'unknown' },
      { date: '2026-10-16', status: 'notObserved', quality: 'verified' }
    ]
  };
  let api: jasmine.SpyObj<CommunityService>;
  let analytics: jasmine.SpyObj<AnalyticsService>;
  let query: BehaviorSubject<ReturnType<typeof convertToParamMap>>;
  const response = (r: CommunityReport = report): CommunityResponse => ({ report: structuredClone(r), members: [structuredClone(member)], nextCursor: null, exclusions: [] });
  beforeEach(() => {
    query = new BehaviorSubject(convertToParamMap({}));
    api = jasmine.createSpyObj('CommunityService', ['report', 'members', 'settings', 'exclusions', 'exclude', 'restore']);
    api.report.and.returnValue(of(response())); api.members.and.returnValue(of(response())); api.exclusions.and.returnValue(of(response()));
    api.settings.and.returnValue(of(response())); api.exclude.and.returnValue(of(response())); api.restore.and.returnValue(of(response()));
    analytics = jasmine.createSpyObj('AnalyticsService', ['trackEvent']);
    TestBed.configureTestingModule({ imports: [CommunityComponent], providers: [
      { provide: CommunityService, useValue: api }, { provide: AnalyticsService, useValue: analytics },
      { provide: AuthService, useValue: { getTwitchIdFromToken: () => '123' } },
      { provide: ActivatedRoute, useValue: { queryParamMap: query } },
      { provide: Router, useValue: { navigate: jasmine.createSpy('navigate') } },
      { provide: StreamerService, useValue: { getAllStreamers: () => of([{ twitchId: 999, displayName: 'Other channel', avatar: '' }]) } }
    ] });
  });
  function create() { const fixture = TestBed.createComponent(CommunityComponent); fixture.detectChanges(); return fixture; }

  it('labels provisional history and never calls chat connection video watching', () => {
    const f = create(); f.detectChanges();
    const text = f.nativeElement.textContent;
    expect(f.nativeElement.querySelector('.method').textContent).toContain('not confirmed video views');
    expect(text).toContain('Preliminary data');
    expect(f.nativeElement.querySelector('.messages').textContent).toBe('—');
    expect(f.nativeElement.querySelector('main').lang).toBe('en');
    f.destroy();
  });
  it('shows recorded message points for the report period rather than a message count', () => {
    const data = response(); data.members[0].messagePoints = 12.5; data.members[0].messageCount = 126;
    api.members.and.returnValue(of(data));
    const f = create(); f.detectChanges();
    expect(f.nativeElement.querySelector('.messages').textContent).toContain('12.50 msg pts');
    expect(f.nativeElement.querySelector('.member-detail').textContent).toContain('Sep 19, 2026');
    expect(f.nativeElement.textContent).not.toContain('126 recorded messages');
    f.destroy();
  });
  it('filters participants when a level is selected without sending participant names to analytics', () => {
    const f = create(); f.componentInstance.selectLevel('regular'); f.detectChanges();
    expect(api.members).toHaveBeenCalledWith('regular', null, null);
    expect(analytics.trackEvent).toHaveBeenCalledWith('community_level_select', { level: 'regular' });
    expect(JSON.stringify(analytics.trackEvent.calls.allArgs())).not.toContain('Quiet');
    f.destroy();
  });
  it('distinguishes unknown history from a confirmed lack of observations', () => {
    const f = create(); f.detectChanges();
    expect(f.nativeElement.querySelector('.history').textContent).toContain('No data');
    expect(f.nativeElement.querySelector('.history').textContent).toContain('Not observed');
    expect(f.nativeElement.querySelector('.history').textContent).toContain('Present');
    f.destroy();
  });
  it('shows unknown metrics as dashes and makes unclassified history accessible', () => {
    api.report.and.returnValue(of(response({ ...report, quality: 'insufficient', core: null, previousCore: null,
      regularIncludingCore: null, firstObserved: null, declining: null, distribution: null, eligibleDays: 3 })));
    const f = create(); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Not enough data');
    expect(f.nativeElement.querySelector('.metric strong').textContent).toBe('—');
    expect(api.members).toHaveBeenCalledWith('', null, null);
    f.destroy();
  });
  it('invalidates the old participant list while an exclusion is recalculated', () => {
    const f = create();
    api.exclude.and.returnValue(of(response({ ...report, recalculating: true, core: null })));
    f.componentInstance.exclusion('1', false); f.detectChanges();
    expect(api.exclude).toHaveBeenCalledWith('1');
    expect(f.componentInstance.members).toEqual([]);
    expect(f.nativeElement.textContent).toContain('Updating report');
    f.destroy();
  });
  it('automatically saves the browser timezone without rendering a settings form', () => {
    api.report.and.returnValue(of(response({ ...report, timeZoneConfigured: false })));
    const f = create(); f.detectChanges();
    expect(api.settings).toHaveBeenCalledOnceWith(browserZone);
    expect(f.nativeElement.querySelector('form')).toBeNull();
    expect(f.componentInstance.report?.timeZoneId).toBe(browserZone);
    f.componentInstance.load();
    expect(api.settings).toHaveBeenCalledTimes(1);
    f.destroy();
  });
  it('keeps the existing report if timezone auto-sync fails', () => {
    api.report.and.returnValue(of(response({ ...report, timeZoneConfigured: false })));
    api.settings.and.returnValue(throwError(() => new Error('unavailable')));
    const f = create();
    f.detectChanges();
    expect(f.nativeElement.querySelector('[role=alert]').textContent).toContain('Could not sync your time zone');
    expect(f.componentInstance.report?.timeZoneId).toBe(browserZone);
    f.destroy();
  });
  it('keeps historical results visible when collection is disabled', () => {
    api.report.and.returnValue(of(response({ ...report, collectionEnabled: false })));
    const f = create(); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Collection paused');
    expect(f.nativeElement.querySelector('.metric strong').textContent).toBe('38');
    f.destroy();
  });
  it('does not republish an old member page after an exclusion invalidates the report', () => {
    const lateMembers = new Subject<CommunityResponse>();
    api.members.and.returnValue(lateMembers);
    const f = create();
    api.exclude.and.returnValue(of(response({ ...report, recalculating: true, core: null })));
    f.componentInstance.exclusion('1', false);
    lateMembers.next(response()); f.detectChanges();
    expect(f.componentInstance.members).toEqual([]);
    expect(f.componentInstance.report?.recalculating).toBeTrue();
    f.destroy();
  });
  it('does not overwrite timezone invalidation with an earlier report request', () => {
    const lateReport = new Subject<CommunityResponse>();
    const lateSettings = new Subject<CommunityResponse>();
    api.report.and.returnValue(lateReport);
    api.settings.and.returnValue(lateSettings);
    const f = create();
    lateReport.next(response({ ...report, timeZoneConfigured: false }));
    lateSettings.next(response({ ...report, recalculating: true, core: null }));
    lateReport.next(response()); f.detectChanges();
    expect(f.componentInstance.report?.recalculating).toBeTrue();
    expect(f.componentInstance.report?.timeZoneId).toBe(browserZone);
    f.destroy();
  });
  it('reads the selected channel without synchronizing its timezone or exposing mutation buttons', () => {
    query.next(convertToParamMap({ channelId: '999' }));
    api.report.and.returnValue(of({ ...response({ ...report, timeZoneConfigured: false }), canViewOtherChannels: true, readOnly: true, channelTwitchUserId: '999' }));
    const f = create(); f.detectChanges();
    expect(api.report).toHaveBeenCalledWith('999');
    expect(api.members).toHaveBeenCalledWith('core', null, '999');
    expect(api.exclusions).toHaveBeenCalledWith('999');
    expect(api.settings).not.toHaveBeenCalled();
    expect(f.nativeElement.querySelector('#community-channel')).not.toBeNull();
    expect(f.nativeElement.textContent).toContain('Read only');
    expect(f.nativeElement.textContent).not.toContain('Exclude account');
    expect(f.nativeElement.textContent).not.toContain('Refresh');
    f.componentInstance.exclusion('1', false);
    expect(api.exclude).not.toHaveBeenCalled();
    f.destroy();
  });
  it('discards an earlier member request when switching channels', () => {
    const oldMembers = new Subject<CommunityResponse>();
    api.members.and.returnValue(oldMembers);
    const f = create();
    api.members.and.returnValue(of({ ...response(), members: [{ ...member, displayName: 'Other member' }] }));
    query.next(convertToParamMap({ channelId: '999' }));
    oldMembers.next(response()); f.detectChanges();
    expect(f.componentInstance.members[0].displayName).toBe('Other member');
    expect(api.members).toHaveBeenCalledWith('core', null, '999');
    f.destroy();
  });
  it('checks a completed report automatically without a Refresh button', fakeAsync(() => {
    const f = create();
    expect(api.report).toHaveBeenCalledTimes(1);
    tick(60000);
    expect(api.report).toHaveBeenCalledTimes(2);
    expect(f.componentInstance.loading).toBeFalse();
    expect(f.nativeElement.querySelector('.heading-actions button')).toBeNull();
    f.destroy();
  }));
});
