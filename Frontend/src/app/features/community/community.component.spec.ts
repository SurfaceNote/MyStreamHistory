import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { NgZone } from '@angular/core';
import { BehaviorSubject, of, Subject, throwError } from 'rxjs';
import { ActivatedRoute, NavigationStart, Router, convertToParamMap } from '@angular/router';
import { LocaleService } from '../../i18n/locale.service';
import { LANGUAGES } from '../../i18n/languages';
import { Chart } from 'chart.js';
import { CommunityComponent } from './community.component';
import { CommunityService } from './community.service';
import { CommunityMember, CommunityMemberFilter, CommunityReport, CommunityResponse } from './community.models';
import { AnalyticsService } from '../../service/analytics.service';
import { AuthService } from '../../auth/auth.service';
import { StreamerService } from '../../service/streamer.service';

describe('Community analytics', () => {
  const browserZone = Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
  const noFilter: CommunityMemberFilter = { segment: '', activity: '', search: '' };
  const segments = { returned: 2, new: 5, lost: 4, fading: 3, core: 38, regular: 52, occasional: 41, oneTime: 60 };
  const report: CommunityReport = {
    methodVersion: 2, calculatedAt: '2026-10-17T08:00:00Z', timeZoneId: browserZone, timeZoneConfigured: true,
    quality: 'provisional', recalculating: false, collectionEnabled: true,
    windowSize: 20, minBroadcasts: 5, totalBroadcasts: 12, skippedBroadcasts: 1, coverage: .98,
    windowDates: ['2026-10-12', '2026-10-14', '2026-10-16'], from: '2026-09-19', asOf: '2026-10-16',
    segments, previousSegments: { ...segments, core: 34, lost: 6 }, previousDate: '2026-10-02',
    activity: { chatty: 20, silent: 9 },
    lastBroadcast: {
      date: '2026-10-16', visitors: 70, newCount: 14, returnedCount: 1, missingCoreCount: 0,
      new: [{ twitchUserId: '5', displayName: 'Fresh' }], returned: [{ twitchUserId: '6', displayName: 'Back again' }], missingCore: []
    },
    retention: { newcomers: 10, cameBack: 4, stayed: 1 },
    trend: [
      { date: '2026-10-14', segments: { ...segments, core: 37 }, visitors: 65, newViewers: 3, returned: 0, becameFading: 1, becameLost: 0, provisional: true },
      { date: '2026-10-16', segments, visitors: 70, newViewers: 14, returned: 1, becameFading: 0, becameLost: 2, provisional: true }
    ]
  };
  const member: CommunityMember = {
    twitchUserId: '1', displayName: 'Quiet', login: 'quiet', profileImageUrl: null, segment: 'core', activity: 'silent',
    visitsInWindow: 2, windowSize: 3, totalVisits: 10, missedStreak: 1, messagesInWindow: null, minutesInWindow: 95,
    firstSeen: '2026-09-20', lastSeen: '2026-10-14', attendance: '110'
  };
  let api: jasmine.SpyObj<CommunityService>;
  let analytics: jasmine.SpyObj<AnalyticsService>;
  let query: BehaviorSubject<ReturnType<typeof convertToParamMap>>;
  const response = (r: CommunityReport = report): CommunityResponse => ({ report: structuredClone(r), members: [structuredClone(member)], membersTotal: 1, nextCursor: null, exclusions: [] });
  const recalculating = (): CommunityReport => ({ ...report, recalculating: true, segments: {}, lastBroadcast: null, trend: [] });
  beforeEach(() => {
    localStorage.removeItem('preferred_language');
    document.cookie = 'preferred_language=; Path=/; Max-Age=0';
    query = new BehaviorSubject(convertToParamMap({}));
    api = jasmine.createSpyObj('CommunityService', ['report', 'members', 'settings', 'exclusions', 'exclude', 'restore']);
    api.report.and.returnValue(of(response())); api.members.and.returnValue(of(response())); api.exclusions.and.returnValue(of(response()));
    api.settings.and.returnValue(of(response())); api.exclude.and.returnValue(of(response())); api.restore.and.returnValue(of(response()));
    analytics = jasmine.createSpyObj('AnalyticsService', ['trackEvent']);
    TestBed.configureTestingModule({ imports: [CommunityComponent], providers: [
      { provide: CommunityService, useValue: api }, { provide: AnalyticsService, useValue: analytics },
      { provide: AuthService, useValue: { getTwitchIdFromToken: () => '123' } },
      { provide: ActivatedRoute, useValue: { queryParamMap: query } },
      { provide: Router, useValue: { url: '/analytics/community', navigated: true, events: new Subject(), navigate: jasmine.createSpy('navigate') } },
      { provide: StreamerService, useValue: { getAllStreamers: () => of([{ twitchId: 999, displayName: 'Other channel', avatar: '' }]) } }
    ] });
  });
  afterEach(() => {
    localStorage.removeItem('preferred_language');
    document.cookie = 'preferred_language=; Path=/; Max-Age=0';
    document.documentElement.lang = 'en';
  });
  function create() { const fixture = TestBed.createComponent(CommunityComponent); fixture.detectChanges(); return fixture; }
  const card = (f: { nativeElement: HTMLElement }, index = 0) => f.nativeElement.querySelectorAll<HTMLElement>('.segment-card')[index];

  it('updates segments, chart, dates and errors in all five languages without reopening the channel', () => {
    const f = create();
    const locale = TestBed.inject(LocaleService);
    (TestBed.inject(Router).events as Subject<NavigationStart>).next(new NavigationStart(1, '/analytics/community'));
    const calls = api.report.calls.count();
    for (const { code } of LANGUAGES) {
      locale.rememberLanguage(code); f.detectChanges();
      expect(f.nativeElement.querySelector('h1').textContent).toBe(locale.t('ui.community'));
      expect(f.nativeElement.querySelector('main').lang).toBe(code);
      expect(card(f).textContent).toContain(locale.t('community.segment.core'));
      expect(card(f).textContent).toContain(locale.t('community.rule.core'));
      const chart = Chart.getChart(f.nativeElement.querySelector('canvas'));
      expect(chart?.data.datasets[0].label).toBe(locale.t('community.segment.core'));
      expect(f.nativeElement.querySelector('.period').textContent).toContain(locale.plural('broadcastCount', 3));
    }
    expect(api.report.calls.count()).toBe(calls);
    api.members.and.returnValue(throwError(() => new Error('Unavailable')));
    f.componentInstance.loadMembers();
    locale.rememberLanguage('ru'); f.detectChanges();
    expect(f.componentInstance.error).toBe('Не удалось загрузить участников. Попробуйте ещё раз.');
    locale.rememberLanguage('de'); f.detectChanges();
    expect(f.componentInstance.error).toBe(locale.t('community.loadMembersFailed'));
    f.destroy();
  });

  it('loads inside NgZone so that clicks in the rendered report trigger change detection', () => {
    let inZone: boolean | null = null;
    api.report.and.callFake(() => { inZone = NgZone.isInAngularZone(); return of(response()); });
    const f = create();
    expect(inZone).toBeTrue();
    f.destroy();
  });

  it('shows segment sizes with the change since ten broadcasts ago', () => {
    const f = create(); f.detectChanges();
    expect(card(f).querySelector('strong')!.textContent).toBe('38');
    expect(card(f).querySelector('.delta')!.textContent).toBe('+4');
    const lost = card(f, 6);
    expect(lost.textContent).toContain('Left');
    expect(lost.querySelector('.delta')!.classList).toContain('down');
    expect(f.nativeElement.querySelector('.compared').textContent).toContain('October 2, 2026');
    f.destroy();
  });

  it('summarises the last broadcast and opens the matching segment', () => {
    const f = create(); f.detectChanges();
    const last = f.nativeElement.querySelector('.last-broadcast');
    expect(last.textContent).toContain('70');
    expect(last.textContent).toContain('Fresh');
    expect(last.textContent).toContain('+13 more');
    expect(last.textContent).toContain('Nobody this time.');
    last.querySelector('.last-count').click();
    expect(api.members).toHaveBeenCalledWith({ segment: 'new', activity: '', search: '' }, null, null);
    f.destroy();
  });

  it('switches chart modes, hides series and stacks them', () => {
    const f = create(); f.detectChanges();
    const chart = () => Chart.getChart(f.nativeElement.querySelector('canvas'))!;
    expect(chart().data.datasets.map(d => d.label)).toEqual(['Core', 'Regulars', 'Occasional', 'Fading']);
    expect(chart().data.datasets[0].data).toEqual([37, 38]);
    f.componentInstance.toggleSeries('fading'); f.componentInstance.toggleSeries('lost');
    expect(chart().data.datasets.map(d => d.label)).toEqual(['Core', 'Regulars', 'Occasional', 'Left']);
    f.componentInstance.toggleStacked();
    expect((chart().options.scales!['y'] as { stacked?: unknown }).stacked).toBeTrue();
    f.componentInstance.setChartMode('flow');
    expect(chart().data.datasets.map(d => d.label)).toEqual(['New', 'Returned', 'Started fading', 'Left']);
    expect(chart().data.datasets[3].data).toEqual([-0, -2]);
    f.componentInstance.setChartMode('visitors');
    expect(chart().data.datasets[0].data).toEqual([65, 70]);
    f.destroy();
  });

  it('never hides the last visible series', () => {
    const f = create();
    for (const id of ['core', 'regular', 'occasional', 'fading'] as const) f.componentInstance.toggleSeries(id);
    expect(f.componentInstance.visibleSeries.size).toBe(1);
    f.destroy();
  });

  it('shows newcomer retention and chat activity filters', () => {
    const f = create(); f.detectChanges();
    const rows = f.nativeElement.querySelectorAll('.funnel-row');
    expect(rows.length).toBe(3);
    expect(rows[1].textContent).toContain('40%');
    f.nativeElement.querySelector('.activity-options button:last-child').click();
    expect(api.members).toHaveBeenCalledWith({ segment: '', activity: 'silent', search: '' }, null, null);
    expect(analytics.trackEvent).toHaveBeenCalledWith('community_activity_select', { activity: 'silent' });
    f.destroy();
  });

  it('labels the method honestly and renders attendance per broadcast', () => {
    const f = create(); f.detectChanges();
    expect(f.nativeElement.querySelector('.method').textContent).toContain('not confirmed video views');
    expect(f.nativeElement.textContent).toContain('Preliminary');
    const strip = f.nativeElement.querySelectorAll('.member .strip i');
    expect(strip.length).toBe(3);
    expect([...strip].map((i: Element) => i.classList.contains('on'))).toEqual([true, true, false]);
    expect(strip[2].getAttribute('title')).toBe('Oct 16');
    expect(f.nativeElement.querySelector('.member .messages').textContent).toBe('—');
    expect(f.nativeElement.querySelector('.member .badges').textContent).toContain('Silent');
    expect(f.nativeElement.querySelector('.member-detail').textContent).toContain('September 20, 2026');
    f.destroy();
  });

  it('filters viewers by segment without sending viewer names to analytics', () => {
    const f = create(); f.componentInstance.selectSegment('regular'); f.detectChanges();
    expect(api.members).toHaveBeenCalledWith({ segment: 'regular', activity: '', search: '' }, null, null);
    expect(analytics.trackEvent).toHaveBeenCalledWith('community_level_select', { level: 'regular' });
    expect(JSON.stringify(analytics.trackEvent.calls.allArgs())).not.toContain('Quiet');
    f.componentInstance.selectSegment('regular');
    expect(api.members).toHaveBeenCalledWith(noFilter, null, null);
    f.destroy();
  });

  it('debounces name search', fakeAsync(() => {
    const f = create();
    api.members.calls.reset();
    f.componentInstance.search('qu'); f.componentInstance.search('qui');
    tick(299);
    expect(api.members).not.toHaveBeenCalled();
    tick(1);
    expect(api.members).toHaveBeenCalledOnceWith({ segment: '', activity: '', search: 'qui' }, null, null);
    f.destroy();
  }));

  it('explains how many broadcasts are still needed before core and regulars appear', () => {
    api.report.and.returnValue(of(response({ ...report, quality: 'insufficient', totalBroadcasts: 2, previousSegments: null, retention: null })));
    const f = create(); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Core and regulars will appear in 3 streams');
    expect(card(f).querySelector('strong')!.textContent).toBe('—');
    expect(card(f, 3).querySelector('strong')!.textContent).toBe('5');
    expect(f.nativeElement.textContent).toContain('Not enough newcomers yet.');
    expect(api.members).toHaveBeenCalledWith(noFilter, null, null);
    f.destroy();
  });

  it('invalidates the old viewer list while an exclusion is recalculated', () => {
    const f = create();
    api.exclude.and.returnValue(of(response(recalculating())));
    f.componentInstance.exclusion('1', false); f.detectChanges();
    expect(api.exclude).toHaveBeenCalledWith('1');
    expect(f.componentInstance.members).toEqual([]);
    expect(f.nativeElement.textContent).toContain('Updating report');
    expect(f.nativeElement.querySelector('canvas')).toBeNull();
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
    expect(card(f).querySelector('strong')!.textContent).toBe('38');
    f.destroy();
  });
  it('does not republish an old member page after an exclusion invalidates the report', () => {
    const lateMembers = new Subject<CommunityResponse>();
    api.members.and.returnValue(lateMembers);
    const f = create();
    api.exclude.and.returnValue(of(response(recalculating())));
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
    lateSettings.next(response(recalculating()));
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
    expect(api.members).toHaveBeenCalledWith(noFilter, null, '999');
    expect(api.exclusions).toHaveBeenCalledWith('999');
    expect(api.settings).not.toHaveBeenCalled();
    expect(f.nativeElement.querySelector('#community-channel')).not.toBeNull();
    expect(f.nativeElement.textContent).toContain('Read only');
    expect(f.nativeElement.textContent).not.toContain('Exclude account');
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
    expect(api.members).toHaveBeenCalledWith(noFilter, null, '999');
    f.destroy();
  });
  it('checks a completed report automatically without a Refresh button', fakeAsync(() => {
    const f = create();
    expect(api.report).toHaveBeenCalledTimes(1);
    tick(60000);
    expect(api.report).toHaveBeenCalledTimes(2);
    expect(f.componentInstance.loading).toBeFalse();
    expect(f.nativeElement.textContent).not.toContain('Refresh');
    f.destroy();
  }));
});
