import { TranslationKey } from '../../i18n/translations';
import { ResponseStatusService } from '../../core/api/response-status.service';
import { LocaleService } from '../../i18n/locale.service';
import { TranslatePipe, LocalPathPipe } from '../../i18n/translate.pipe';
import { GameArtComponent } from '../../components/game-art/game-art.component';
import {
  afterNextRender,
  PLATFORM_ID,
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  ElementRef,
  inject,
  OnDestroy,
  OnInit,
  ViewChild,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../auth/auth.service';
import { StreamerService } from '../../service/streamer.service';
import { StreamerShortDTO } from '../../models/streamer-short.dto';
import {
  catchError,
  finalize,
  forkJoin,
  map,
  of,
  Subscription,
  switchMap,
  tap,
} from 'rxjs';
import { StreamSession } from '../../models/stream-session.model';
import { ViewerStats } from '../../models/viewer-stats.model';
import { isPlatformBrowser, CommonModule, Location } from '@angular/common';
import { SocialLink } from '../../models/social-link.model';
import {
  StreamerDashboardPeriod,
  StreamerStatistics,
  TimeSeriesPoint,
} from '../../models/streamer-statistics.model';
import { PlaythroughStatistics } from '../../models/playthrough-statistics.model';
import { CategoryStatistics } from '../../models/category-statistics.model';
import { Chart, registerables } from 'chart.js';
import { SeoService } from '../../service/seo.service';
import { AnalyticsService } from '../../service/analytics.service';
import { SettingsService } from '../../service/settings.service';
import { CollectionStatus } from '../../models/collection-status.model';
import { Playthrough, PlaythroughGameOption, PlaythroughSettings, PlaythroughStatus } from '../../models/playthrough.model';

Chart.register(...registerables);

interface PlaythroughStatusSection {
  status: string;
  title: string;
  items: PlaythroughStatistics[];
  total?: number;
}

interface GameProgressItem {
  status: string;
  count: number;
}

const PROGRESS_ORDER = ['Playing', 'Completed', 'Planned', 'Dropped'];
const NO_PROGRESS: GameProgressItem[] = [];

interface OwnerChecklistStep {
  id: 'playthroughs' | 'profile';
  label: TranslationKey;
  done: boolean;
}

const PROFILE_TABS: Record<string, string> = { streams: 'Streams', games: 'Games', viewers: 'Viewers' };
const GAME_STATUSES: Record<string, string> = { playing: 'Playing', completed: 'Completed', planned: 'Planned', dropped: 'Dropped' };
const OWNER_PANEL_HIDDEN_KEY = 'msh.ownerPanel.hidden';
const DISMISSED_SUGGESTIONS_KEY = 'msh.playthroughSuggestions.dismissed';
const OVERVIEW_SHELF_LIMIT = 8;
const FIRST_STREAMS_PAGE = 10;
const OLDER_STREAMS_PAGE = 20;

interface DashboardSummaryCard {
  icon: string;
  label: string;
  value: string;
  hint: string;
}

@Component({
  selector: 'app-streamer-profile',
  imports: [TranslatePipe, LocalPathPipe, GameArtComponent, CommonModule, RouterLink],
  templateUrl: './streamer-profile.component.html',
  styleUrl: './streamer-profile.component.scss',
})
export class StreamerProfileComponent
  implements OnInit, AfterViewInit, OnDestroy
{
  private readonly responseStatus = inject(ResponseStatusService);
  readonly Math = Math;
  readonly locale = inject(LocaleService);
  @ViewChild('streamedHoursChart')
  streamedHoursChart?: ElementRef<HTMLCanvasElement>;

  readonly socialNetworks = [
    { name: 'YouTube', icon: 'youtube', color: '#e00022' },
    { name: 'Discord', icon: 'discord', color: '#5865f2' },
    { name: 'Instagram', icon: 'instagram', color: '#c13584' },
    { name: 'Steam', icon: 'steam', color: '#23445a' },
    { name: 'VK', icon: 'vk', color: '#0077ff' },
    { name: 'Yandex', icon: 'yandex', color: '#d93025' },
    { name: 'Telegram', icon: 'telegram', color: '#229ed9' },
  ];
  profileLoadFailed = false;
  activeSection = 'Overview';
  sections = ['Overview', 'Streams', 'Games', 'Viewers'];
  gameStatus = 'All';
  readonly gameStatuses = [
    { value: 'All', label: this.locale.t('ui.allGames') },
    { value: 'Playing', label: this.locale.t('ui.playingNow') },
    { value: 'Completed', label: this.locale.t('ui.completed') },
    { value: 'Planned', label: this.locale.t('ui.planned') },
    { value: 'Dropped', label: this.locale.t('ui.dropped') },
  ];
  gameQuery = '';
  gameSort = 'hours';

  get featuredStream(): StreamSession | undefined {
    return (
      this.recentStreams.find((stream) => stream.isLive) ??
      this.recentStreams[0]
    );
  }

  get filteredGames() {
    const query = this.gameQuery.trim().toLowerCase();
    return [...(this.statistics?.categories ?? [])]
      .filter((game) => game.name.toLowerCase().includes(query))
      .sort((a, b) =>
        this.gameSort === 'name'
          ? a.name.localeCompare(b.name)
          : b.totalHours - a.totalHours,
      );
  }

  selectSection(section: string): void {
    this.activeSection = section;
    if (section === 'Overview') this.scheduleDashboardRender();
    this.syncTabUrl();
    this.analytics.trackEvent('profile_tab_select', {
      tab_name: section.toLowerCase(),
    });
  }

  selectGameStatus(status: string): void {
    this.gameStatus = status;
    this.syncTabUrl();
  }

  /** Keeps the open tab shareable (?tab=games&status=completed) without a router navigation. */
  private syncTabUrl(): void {
    if (!this.browser) return;
    const tree = this.router.parseUrl(this.location.path(true));
    const params = { ...tree.queryParams };
    delete params['tab'];
    delete params['status'];
    if (this.activeSection !== 'Overview') params['tab'] = this.activeSection.toLowerCase();
    if (this.activeSection === 'Games' && this.gameStatus !== 'All') params['status'] = this.gameStatus.toLowerCase();
    tree.queryParams = params;
    this.location.replaceState(this.router.serializeUrl(tree));
  }

  private applyTabFromUrl(tab: string | null, status: string | null): void {
    const section = PROFILE_TABS[(tab ?? '').toLowerCase()] ?? 'Overview';
    this.gameStatus = section === 'Games' ? GAME_STATUSES[(status ?? '').toLowerCase()] ?? 'All' : 'All';
    if (section !== this.activeSection) {
      this.activeSection = section;
      if (section === 'Overview') this.scheduleDashboardRender();
    }
  }

  gameShare(hours: number): number {
    const maximum = Math.max(
      ...(this.statistics?.dashboard.topCategories ?? []).map(
        (game) => game.totalHours,
      ),
      1,
    );
    return (hours / maximum) * 100;
  }

  twitchId!: number;
  streamerShortDTO!: StreamerShortDTO;
  recentStreams: StreamSession[] = [];
  topViewers: ViewerStats[] = [];
  socialLinks: SocialLink[] = [];
  statistics: StreamerStatistics | null = null;
  selectedDashboardPeriod: StreamerDashboardPeriod = '30d';
  dashboardPeriods: Array<{ value: StreamerDashboardPeriod; label: string }> = [
    { value: '7d', label: this.locale.t('ui.7Days') },
    { value: '30d', label: this.locale.t('ui.30Days') },
    { value: '90d', label: this.locale.t('ui.90Days') },
  ];
  isLoadingStreams: boolean = false;
  isLoadingViewers: boolean = false;
  isLoadingSocialLinks: boolean = false;
  isLoadingStatistics: boolean = false;
  private routeSub: Subscription | null = null;
  private queryParamsSub: Subscription | null = null;
  private ownerStatusSub: Subscription | null = null;
  hasOlderStreams = false;
  isLoadingOlderStreams = false;
  olderStreamsFailed = false;
  collectionStatus: CollectionStatus | null = null;
  collectionStatusFailed = false;
  ownerPanelHidden = false;
  ownerPlaythroughs: PlaythroughSettings | null = null;
  private ownerPlaythroughsSub: Subscription | null = null;
  private dismissedSuggestions = new Set<string>();
  /** Suggestion or playthrough currently being saved. */
  savingId: string | null = null;
  shelfMessage: { key: TranslationKey; params?: Record<string, string | number>; error?: boolean } | null = null;
  readonly shelfStatuses: PlaythroughStatus[] = ['Playing', 'Planned', 'Completed', 'Dropped'];
  private recentStreamsSub: Subscription | null = null;
  private charts: Chart[] = [];

  private streamerService = inject(StreamerService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private changeDetector = inject(ChangeDetectorRef);
  private seo = inject(SeoService);
  private analytics = inject(AnalyticsService);
  private auth = inject(AuthService);
  private settings = inject(SettingsService);
  private location = inject(Location);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private authReady = false;
  constructor() {
    afterNextRender(() => {
      this.authReady = true;
      this.ownerPanelHidden = this.readOwnerPanelHidden();
      this.dismissedSuggestions = this.readDismissedSuggestions();
      this.loadOwnerStatus();
      this.loadOwnerPlaythroughs();
      this.changeDetector.markForCheck();
    });
  }
  readonly sectionLabels: Record<string, TranslationKey> = { Overview: 'ui.overview', Streams: 'ui.streamsLabel', Games: 'ui.gamesLabel', Viewers: 'ui.viewers' };
  readonly sectionIcons: Record<string, string> = {
    Overview: 'fa-solid fa-chart-simple',
    Streams: 'fa-solid fa-tower-broadcast',
    Games: 'fa-solid fa-gamepad',
    Viewers: 'fa-solid fa-users',
  };
  readonly statusIcons: Record<string, string> = {
    Playing: 'fa-solid fa-play',
    Completed: 'fa-solid fa-trophy',
    Planned: 'fa-solid fa-bookmark',
    Dropped: 'fa-solid fa-pause',
  };

  /** Shown next to a tab label; drawn by CSS so the tab name stays the button text. */
  sectionCount(section: string): string | null {
    const count = section === 'Streams' ? this.statistics?.totalStreamsCount
      : section === 'Games' ? this.statistics?.categories?.length
      : undefined;
    return count ? this.locale.number(count, 0) : null;
  }

  statusCount(status: string): number {
    if (status === 'All') return this.statistics?.categories?.length ?? 0;
    return (this.statistics?.playthroughs ?? []).filter((game) => game.status === status).length;
  }

  /** The overview shows the most streamed games first, whatever order the API returns. */
  get topGames() {
    return [...(this.statistics?.categories ?? [])]
      .sort((a, b) => b.totalHours - a.totalHours)
      .slice(0, 6);
  }

  /**
   * Shelf state of a game for the collection tiles: in progress, finished (and how many times), planned, dropped.
   * Cached per statistics response, since the template asks for every tile on each check.
   */
  gameProgress(game: CategoryStatistics): GameProgressItem[] {
    if (this.progressSource !== this.statistics) {
      this.progressSource = this.statistics;
      this.progressByGame.clear();
      for (const playthrough of this.statistics?.playthroughs ?? []) {
        for (const key of [playthrough.twitchCategoryId, 'twitch:' + playthrough.twitchCategoryTwitchId]) {
          const counts = this.progressByGame.get(key) ?? new Map<string, number>();
          counts.set(playthrough.status, (counts.get(playthrough.status) ?? 0) + 1);
          this.progressByGame.set(key, counts);
        }
      }
    }
    const counts = this.progressByGame.get(game.twitchCategoryId) ?? this.progressByGame.get('twitch:' + game.twitchId);
    if (!counts) return NO_PROGRESS;
    return PROGRESS_ORDER.filter((status) => counts.has(status)).map((status) => ({ status, count: counts.get(status)! }));
  }

  private progressSource: StreamerStatistics | null = null;
  private readonly progressByGame = new Map<string, Map<string, number>>();

  /** The podium takes the first three places on the Viewers tab. */
  get viewerRankOffset(): number {
    return this.activeSection === 'Viewers' && this.topViewers.length >= 3 ? 3 : 0;
  }

  readonly statusLabels: Record<string, TranslationKey> = { Playing: 'ui.playing', Completed: 'ui.completed', Planned: 'ui.planned', Dropped: 'ui.dropped' };
  get isOwnProfile(): boolean { return this.authReady && this.auth.getTwitchIdFromToken() === String(this.twitchId); }
  get canViewCommunity(): boolean { return this.authReady && (this.isOwnProfile); }

  ngOnInit(): void {
    this.routeSub = this.route.paramMap.subscribe((params) => {
      const idParam = params.get('twitchId');
      if (idParam) {
        this.twitchId = +idParam;
        this.loadStreamer();
        this.loadRecentStreams();
        this.loadTopViewers();
        this.loadSocialLinks();
        this.loadStatistics();
        if (this.authReady) { this.loadOwnerStatus(); this.loadOwnerPlaythroughs(); }
      }
    });
    this.queryParamsSub = this.route.queryParamMap.subscribe((query) =>
      this.applyTabFromUrl(query.get('tab'), query.get('status')),
    );
  }

  // ---- Owner panel: first-stream notice and setup steps, only on the owner's own profile.

  get hasRecordedStreams(): boolean {
    return (this.collectionStatus?.streamsCount ?? 0) > 0;
  }

  /** A new channel learns that recording starts with its next stream. */
  get showWaitingIntro(): boolean {
    return !!this.collectionStatus && !this.hasRecordedStreams;
  }

  get ownerChecklist(): OwnerChecklistStep[] {
    return [
      { id: 'playthroughs', label: 'owner.stepPlaythrough', done: (this.statistics?.playthroughs?.length ?? 0) > 0 },
      { id: 'profile', label: 'owner.stepSocial', done: this.socialLinks.length > 0 },
    ];
  }

  get showOwnerPanel(): boolean {
    if (!this.isOwnProfile || (!this.collectionStatus && !this.collectionStatusFailed)) return false;
    if (this.showWaitingIntro) return true;
    return !this.ownerPanelHidden && this.ownerChecklist.some((step) => !step.done);
  }

  get canHideOwnerPanel(): boolean {
    return !this.showWaitingIntro;
  }

  hideOwnerPanel(): void {
    this.ownerPanelHidden = true;
    this.auth.clearNewChannel();
    try { localStorage.setItem(OWNER_PANEL_HIDDEN_KEY, '1'); } catch { /* Storage is optional. */ }
  }

  private readOwnerPanelHidden(): boolean {
    if (!this.browser) return false;
    try { return localStorage.getItem(OWNER_PANEL_HIDDEN_KEY) === '1'; } catch { return false; }
  }

  loadOwnerStatus(): void {
    this.ownerStatusSub?.unsubscribe();
    this.collectionStatus = null;
    this.collectionStatusFailed = false;
    if (!this.browser || !this.isOwnProfile) return;
    this.ownerStatusSub = this.settings.getCollectionStatus().subscribe({
      next: (status) => {
        this.collectionStatus = status;
        if (status.streamsCount > 0) this.auth.clearNewChannel();
        this.changeDetector.markForCheck();
      },
      error: (err) => {
        console.error('Error loading recording status', err);
        this.collectionStatusFailed = true;
        this.changeDetector.markForCheck();
      },
    });
  }

  // ---- Stream archive: the first page includes chat account counts, older pages are listed without them.

  loadOlderStreams(): void {
    const oldest = this.recentStreams[this.recentStreams.length - 1];
    if (!oldest || this.isLoadingOlderStreams) return;
    this.isLoadingOlderStreams = true;
    this.olderStreamsFailed = false;
    this.streamerService
      .getRecentStreams(this.twitchId, OLDER_STREAMS_PAGE, oldest.startedAt)
      .pipe(finalize(() => { this.isLoadingOlderStreams = false; }))
      .subscribe({
        next: (streams) => {
          const known = new Set(this.recentStreams.map((stream) => stream.id));
          this.recentStreams = [...this.recentStreams, ...streams.filter((stream) => !known.has(stream.id))];
          this.hasOlderStreams = streams.length === OLDER_STREAMS_PAGE;
        },
        error: (err) => {
          console.error('Error loading older streams', err);
          this.olderStreamsFailed = true;
        },
      });
  }

  get overviewPlaythroughSections(): PlaythroughStatusSection[] {
    return this.playthroughSections
      .filter((section) => ['Playing', 'Completed'].includes(section.status))
      .map((section) => ({ ...section, total: section.items.length, items: section.items.slice(0, OVERVIEW_SHELF_LIMIT) }));
  }

  // ---- Shelf: playthrough posters. The owner can change a status in place and add suggested games.

  shelfDate(value: string): string {
    return this.formatShelfDate(value);
  }

  private formatShelfDate(value: string): string {
    const date = new Date(value);
    const sameYear = date.getUTCFullYear() === new Date().getUTCFullYear();
    return date.toLocaleDateString(this.locale.locale(), sameYear
      ? { timeZone: 'UTC', month: 'short', day: 'numeric' }
      : { timeZone: 'UTC', month: 'short', day: 'numeric', year: 'numeric' });
  }

  ownerPlaythrough(game: PlaythroughStatistics): Playthrough | undefined {
    return this.isOwnProfile ? this.ownerPlaythroughs?.playthroughs.find((item) => item.id === game.playthroughId) : undefined;
  }

  get playthroughSuggestions(): PlaythroughGameOption[] {
    if (!this.isOwnProfile) return [];
    return (this.ownerPlaythroughs?.availableGames ?? [])
      .filter((game) => game.suggested && !this.dismissedSuggestions.has(game.twitchCategoryId))
      .slice(0, 4);
  }

  loadOwnerPlaythroughs(): void {
    this.ownerPlaythroughsSub?.unsubscribe();
    this.ownerPlaythroughs = null;
    if (!this.browser || !this.isOwnProfile) return;
    this.ownerPlaythroughsSub = this.settings.getPlaythroughs().subscribe({
      next: (settings) => {
        this.ownerPlaythroughs = settings;
        this.changeDetector.markForCheck();
      },
      error: (err) => console.error('Error loading playthrough settings', err),
    });
  }

  changeShelfStatus(game: PlaythroughStatistics, status: string): void {
    const playthrough = this.ownerPlaythrough(game);
    if (!playthrough || playthrough.status === status || this.savingId) return;
    const previous = game.status;
    game.status = status;
    this.savingId = playthrough.id;
    this.shelfMessage = null;
    this.settings.savePlaythrough({
      id: playthrough.id,
      twitchCategoryId: playthrough.twitchCategoryId,
      title: playthrough.title,
      status: status as PlaythroughStatus,
      autoAddNewStreams: playthrough.autoAddNewStreams,
      streamCategoryIds: playthrough.streamCategories.map((segment) => segment.streamCategoryId),
    }).pipe(finalize(() => { this.savingId = null; })).subscribe({
      next: () => {
        this.shelfMessage = { key: status === 'Completed' ? 'shelf.completedSaved' : 'shelf.statusSaved', params: { game: game.gameName } };
        this.analytics.trackEvent('playthrough_status_changed', { status: status.toLowerCase(), source: 'profile' });
        this.refreshShelf();
      },
      error: (err) => {
        console.error('Error changing playthrough status', err);
        game.status = previous;
        this.shelfMessage = { key: 'shelf.statusFailed', error: true };
      },
    });
  }

  addSuggestedPlaythrough(game: PlaythroughGameOption, status: PlaythroughStatus): void {
    if (this.savingId) return;
    this.savingId = game.twitchCategoryId;
    this.shelfMessage = null;
    this.settings.savePlaythrough({
      twitchCategoryId: game.twitchCategoryId,
      status,
      autoAddNewStreams: status === 'Playing',
      streamCategoryIds: game.streamCategories.map((segment) => segment.streamCategoryId),
    }).pipe(finalize(() => { this.savingId = null; })).subscribe({
      next: () => {
        const key: TranslationKey = status === 'Completed' ? 'suggest.addedCompleted' : status === 'Dropped' ? 'suggest.addedDropped' : 'suggest.added';
        this.shelfMessage = { key, params: { game: game.name } };
        this.analytics.trackEvent('playthrough_created', { source: 'suggested', status: status.toLowerCase() });
        this.refreshShelf();
      },
      error: (err) => {
        console.error('Error adding suggested playthrough', err);
        this.shelfMessage = { key: 'suggest.failed', error: true };
      },
    });
  }

  dismissSuggestion(game: PlaythroughGameOption): void {
    this.dismissedSuggestions.add(game.twitchCategoryId);
    try { localStorage.setItem(DISMISSED_SUGGESTIONS_KEY, JSON.stringify([...this.dismissedSuggestions])); } catch { /* Storage is optional. */ }
  }

  private readDismissedSuggestions(): Set<string> {
    if (!this.browser) return new Set();
    try {
      const value: unknown = JSON.parse(localStorage.getItem(DISMISSED_SUGGESTIONS_KEY) ?? '[]');
      return new Set(Array.isArray(value) ? value.filter((id): id is string => typeof id === 'string') : []);
    } catch { return new Set(); }
  }

  /** Reloads the shelf without the loading state, so the page does not jump. */
  private refreshShelf(): void {
    this.loadOwnerPlaythroughs();
    this.streamerService.getStreamerStatistics(this.twitchId, this.selectedDashboardPeriod).subscribe({
      next: (data) => {
        this.statistics = data;
        this.changeDetector.markForCheck();
        this.scheduleDashboardRender();
      },
      error: (err) => console.error('Error refreshing statistics', err),
    });
  }


  get selectedPlaythroughs(): PlaythroughStatistics[] {
    const query = this.gameQuery.trim().toLowerCase();
    return (this.statistics?.playthroughs ?? [])
      .filter(
        (game) =>
          game.status === this.gameStatus &&
          game.gameName.toLowerCase().includes(query),
      )
      .sort((a, b) =>
        this.gameSort === 'name'
          ? a.gameName.localeCompare(b.gameName)
          : b.totalHours - a.totalHours,
      );
  }

  get playthroughSections(): PlaythroughStatusSection[] {
    const playthroughs = this.statistics?.playthroughs ?? [];

    const sections: Array<{ status: string; title: string }> = [
      { status: 'Playing', title: this.locale.t('ui.playingNow') },
      { status: 'Planned', title: this.locale.t('ui.willPlay') },
      { status: 'Dropped', title: this.locale.t('ui.dropped') },
      { status: 'Completed', title: this.locale.t('ui.completed') },
    ];

    return sections
      .map((section) => ({
        ...section,
        items: playthroughs.filter(
          (playthrough) => playthrough.status === section.status,
        ),
      }))
      .filter((section) => section.items.length > 0);
  }

  ngAfterViewInit(): void {
    this.renderDashboardCharts();
  }

  get dashboardSummaryCards(): DashboardSummaryCard[] {
    const dashboard = this.statistics?.dashboard;
    const streamedDays = (dashboard?.streamedHoursByDay ?? []).filter(
      (point) => point.value > 0,
    ).length;
    const topCategory = dashboard?.topCategories?.[0];

    return [
      {
        icon: 'M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18 M12 7v5l4 2',
        label: this.locale.t('ui.hoursLiveLabel'),
        value: `${this.formatNumber(dashboard?.totalStreamedHours ?? 0)} ${this.locale.t('unit.hours')}`,
        hint: this.selectedPeriodLabel,
      },
      {
        icon: 'M4 20V10h3v10H4Zm7 0V6h3v14h-3Zm7 0V2h3v18h-3Z',
        label: this.locale.t('ui.streamsLabel'),
        value: `${dashboard?.totalStreamsCount ?? 0}`,
        hint: dashboard?.streamedHoursByDay?.length ? this.locale.plural('dayCount', streamedDays) : this.locale.t('ui.dailyHistoryUnavailable'),
      },
      {
        icon: 'M7 7h10c3 0 4 4 5 10 .5 3-2 4-4 1l-2-2H8l-2 2c-2 3-4 2-4-1 1-6 2-10 5-10Z M7 10v5m-2.5-2.5h5m6-1h.1m3 3h.1',
        label: this.locale.t('ui.gamesPlayed'),
        value: `${dashboard?.totalUniqueGamesCount ?? 0}`,
        hint: topCategory ? this.locale.t('lead', { name: topCategory.name }) : this.locale.t('ui.noCategoriesYet'),
      },
      {
        icon: 'M5 5h14v16H5V5Zm3-3v6m8-6v6M5 10h14M8 14h1m3 0h1m3 0h1m-9 4h1m3 0h1',
        label: this.locale.t('ui.activeDays'),
        value: dashboard?.streamedHoursByDay?.length ? `${streamedDays}` : '—',
        hint: this.locale.t('ui.daysWithABroadcast'),
      },
      {
        icon: 'M6 3h12M6 21h12M7 3v5l10 8v5M17 3v5L7 16v5M7 7h10',
        label: this.locale.t('ui.averageStream'),
        value: dashboard?.totalStreamsCount ? `${this.formatNumber(dashboard.totalStreamedHours / dashboard.totalStreamsCount)} ${this.locale.t('unit.hours')}` : '—',
        hint: this.locale.t('ui.hoursPerBroadcast'),
      },
      // Shown only when the server reports chat activity; a missing series is not a zero.
      ...(dashboard?.chatPointsByDay?.length ? [{
        icon: 'M4 4h16v12H9l-5 4V4Zm4 5h8m-8 3h5',
        label: this.locale.t('ui.chatPoints'),
        value: this.formatNumber(dashboard.chatPointsByDay.reduce((sum, point) => sum + point.value, 0)),
        hint: this.locale.t('ui.recordedChatActivity'),
      }] : []),
    ];
  }

  get activityDays(): Array<{ date: string; label: string; hours: number }> {
    return (this.statistics?.dashboard?.streamedHoursByDay ?? []).slice(-35).map(point => ({
      date: point.date,
      label: point.date.slice(8, 10).replace(/^0/, ''),
      hours: point.value,
    }));
  }

  get activityRange(): string {
    const days = this.activityDays;
    return days.length ? `${this.formatSeriesDate(days[0].date)} – ${this.formatSeriesDate(days[days.length - 1].date)}` : this.selectedPeriodLabel;
  }

  get selectedPeriodLabel(): string {
    return (
      this.dashboardPeriods.find(
        (period) => period.value === this.selectedDashboardPeriod,
      )?.label ?? this.locale.t('ui.30Days')
    );
  }

  loadSocialLinks(): void {
    this.isLoadingSocialLinks = true;
    this.streamerService.getSocialLinksByTwitchId(this.twitchId).subscribe({
      next: (response) => {
        this.socialLinks = response.socialLinks;
        this.isLoadingSocialLinks = false;
      },
      error: (err) => {
        console.error('Error loading social links', err);
        this.isLoadingSocialLinks = false;
      },
    });
  }

  getSocialLinkUrl(type: string): string | null {
    const link = this.socialLinks.find((l) => l.socialNetworkType === type);
    return link ? link.fullUrl : null;
  }

  ngOnDestroy(): void {
    if (this.routeSub) {
      this.routeSub.unsubscribe();
    }
    this.queryParamsSub?.unsubscribe();
    this.ownerStatusSub?.unsubscribe();
    this.ownerPlaythroughsSub?.unsubscribe();

    this.recentStreamsSub?.unsubscribe();

    this.destroyDashboardCharts();
  }

  loadStreamer(): void {
    this.profileLoadFailed = false;
    this.streamerService.getStreamerByTwitchId(this.twitchId).subscribe({
      next: (data: StreamerShortDTO) => {
        this.responseStatus.reset();
        this.streamerShortDTO = data;
        this.seo.update({
          title: this.locale.t('seo.profileTitle', { name: data.displayName }),
          description: this.locale.t('seo.profileDescription', { name: data.displayName }),
          image: data.avatar,
          type: 'profile',
        });
      },
      error: (err) => {
        this.responseStatus.fail(err);
        this.seo.update({ title: this.locale.t(err.status === 404 ? 'error.notFound' : 'error.unavailable'), description: this.locale.t('error.unavailable'), noIndex: true });
        this.profileLoadFailed = true;
        console.error('Error loading streamer', err);
      },
    });
  }

  loadRecentStreams(): void {
    // Cancel the previous channel's list and any in-flight viewer requests
    // before resetting loading state (unsubscribe also runs finalize).
    this.recentStreamsSub?.unsubscribe();
    this.recentStreams = [];
    this.hasOlderStreams = false;
    this.olderStreamsFailed = false;
    this.isLoadingStreams = true;
    this.recentStreamsSub = this.streamerService
      .getRecentStreams(this.twitchId, FIRST_STREAMS_PAGE)
      .pipe(
        tap((streams) => {
          // The featured broadcast must not wait for every viewer request.
          this.recentStreams = streams;
          this.hasOlderStreams = streams.length === FIRST_STREAMS_PAGE;
          this.isLoadingStreams = false;
        }),
        switchMap((streams: StreamSession[]) => {
          if (streams.length === 0) {
            return of([] as StreamSession[]);
          }

          return forkJoin(
            streams.map((stream) =>
              this.streamerService.getStreamDetails(stream.id).pipe(
                map((details) => ({
                  ...stream,
                  uniqueViewersCount: details.viewerDataAvailable === false ? null : details.viewers.length,
                })),
                catchError((err) => {
                  console.error(
                    `Error loading unique viewers for stream ${stream.id}`,
                    err,
                  );
                  return of({ ...stream, uniqueViewersCount: null });
                }),
              ),
            ),
          );
        }),
        finalize(() => {
          this.isLoadingStreams = false;
        }),
      )
      .subscribe({
        next: (data: StreamSession[]) => {
          // Older pages may have been appended while chat account counts were loading.
          const counted = new Map(data.map((stream) => [stream.id, stream]));
          this.recentStreams = this.recentStreams.map((stream) => counted.get(stream.id) ?? stream);
        },
        error: (err) => {
          console.error('Error loading recent streams', err);
        },
      });
  }

  formatDate(dateString: string): string {
    const date = new Date(dateString);
    return date.toLocaleDateString(this.locale.locale(), {
      timeZone: 'UTC', day: '2-digit',
      month: '2-digit',
      year: 'numeric',
    });
  }

  formatDuration(startedAt: string, endedAt?: string): string {
    const start = new Date(startedAt);
    const end = endedAt ? new Date(endedAt) : new Date();
    const durationMs = end.getTime() - start.getTime();
    const hours = Math.floor(durationMs / (1000 * 60 * 60));
    const minutes = Math.floor((durationMs % (1000 * 60 * 60)) / (1000 * 60));

    return this.locale.duration(hours * 60 + minutes);
  }

  getCategoryBoxArt(boxArtUrl: string, width: number, height: number): string {
    return boxArtUrl
      .replace('{width}', width.toString())
      .replace('{height}', height.toString());
  }

  loadTopViewers(): void {
    this.isLoadingViewers = true;
    this.streamerService.getTopViewers(this.twitchId, 100).subscribe({
      next: (data: ViewerStats[]) => {
        this.topViewers = data;
        this.isLoadingViewers = false;
      },
      error: (err) => {
        console.error('Error loading top viewers', err);
        this.isLoadingViewers = false;
      },
    });
  }

  formatHours(minutes: number): string {
    const hours = Math.floor(minutes / 60);
    return this.locale.number(hours, 0) + ' ' + this.locale.t('unit.hours');
  }

  formatMessagePoints(points: number): string {
    return this.locale.number(points, 1);
  }

  getDefaultAvatar(): string {
    return 'https://static-cdn.jtvnw.net/user-default-pictures-uv/ebe4cd89-b4f4-4cd9-adac-2f30151b4209-profile_image-70x70.png';
  }

  navigateToStreamDetail(streamId: string): void {
    this.router.navigateByUrl(this.locale.path(['/stream', streamId]));
  }

  private streamCardPointer?: {
    card: EventTarget | null;
    pointerId: number;
    x: number;
    y: number;
    moved: boolean;
  };

  startStreamCardPointer(event: PointerEvent): void {
    this.streamCardPointer = {
      card: event.currentTarget, pointerId: event.pointerId,
      x: event.clientX, y: event.clientY, moved: false,
    };
  }

  moveStreamCardPointer(event: PointerEvent): void {
    const pointer = this.streamCardPointer;
    if (pointer?.pointerId === event.pointerId &&
        Math.hypot(event.clientX - pointer.x, event.clientY - pointer.y) > 8) {
      pointer.moved = true;
    }
  }

  cancelStreamCardPointer(): void {
    if (this.streamCardPointer) this.streamCardPointer.moved = true;
  }

  openStreamCard(event: MouseEvent): void {
    const pointer = this.streamCardPointer;
    this.streamCardPointer = undefined;
    if (pointer?.card === event.currentTarget && pointer.moved) {
      event.preventDefault();
      return;
    }
    // Keep the actual link responsible for routing and modifier-key behavior.
    if (event.defaultPrevented || (event.target as Element).closest('a')) return;
    const link = (event.currentTarget as HTMLElement).querySelector<HTMLAnchorElement>('.stream-top');
    link?.dispatchEvent(new MouseEvent('click', {
      bubbles: true, cancelable: true, button: event.button,
      ctrlKey: event.ctrlKey, metaKey: event.metaKey,
      shiftKey: event.shiftKey, altKey: event.altKey,
    }));
  }

  navigateToViewerStats(viewer: ViewerStats): void {
    const viewerTwitchId = viewer.viewer?.twitchUserId;
    if (viewerTwitchId) {
      this.router.navigateByUrl(this.locale.path([
        '/profile',
        this.twitchId,
        'viewer',
        viewerTwitchId,
      ]));
    }
  }

  loadStatistics(): void {
    this.isLoadingStatistics = true;
    this.streamerService
      .getStreamerStatistics(this.twitchId, this.selectedDashboardPeriod)
      .subscribe({
        next: (data: StreamerStatistics) => {
          this.statistics = data;
          this.isLoadingStatistics = false;
          this.changeDetector.detectChanges();
          this.scheduleDashboardRender();
        },
        error: (err) => {
          console.error('Error loading statistics', err);
          this.isLoadingStatistics = false;
        },
      });
  }

  getCategoryBoxArtForStats(
    boxArtUrl: string,
    width: number,
    height: number,
  ): string {
    return boxArtUrl
      .replace('{width}', width.toString())
      .replace('{height}', height.toString());
  }

  selectDashboardPeriod(period: StreamerDashboardPeriod): void {
    if (this.selectedDashboardPeriod === period) {
      return;
    }

    this.selectedDashboardPeriod = period;
    this.loadStatistics();
  }

  hasSeriesData(series?: TimeSeriesPoint[]): boolean {
    return (series ?? []).some((point) => point.value > 0);
  }

  hasSeriesPoints(series?: TimeSeriesPoint[]): boolean {
    return (series ?? []).length > 0;
  }

  formatNumber(value: number): string {
    return this.locale.number(value);
  }

  formatSeriesDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString(this.locale.locale(), {
      timeZone: 'UTC', month: 'short',
      day: 'numeric',
    });
  }

  private scheduleDashboardRender(): void {
    if (this.browser) requestAnimationFrame(() => this.renderDashboardCharts());
  }

  private renderDashboardCharts(): void {
    if (!this.browser) return;
    const dashboard = this.statistics?.dashboard;
    if (!dashboard) {
      return;
    }

    this.destroyDashboardCharts();

    if (
      this.streamedHoursChart &&
      this.hasSeriesPoints(dashboard.streamedHoursByDay)
    ) {
      this.charts.push(
        this.createHoursChart(
          this.streamedHoursChart.nativeElement,
          dashboard.streamedHoursByDay,
          'Streamed hours',
          '#a78bfa',
        ),
      );
    }
  }

  private createHoursChart(
    canvas: HTMLCanvasElement,
    series: TimeSeriesPoint[],
    label: string,
    color: string,
  ): Chart {
    return new Chart(canvas, {
      type: 'bar',
      data: {
        labels: series.map((point) => this.formatSeriesDate(point.date)),
        datasets: [
          {
            label,
            data: series.map((point) => point.value),
            borderColor: color,
            backgroundColor: color,
            borderWidth: 0,
            borderRadius: 4,
            maxBarThickness: 22,
          },
        ],
      },
      options: this.baseCartesianOptions(),
    });
  }

  private baseCartesianOptions(): any {
    return this.baseChartOptions({
      scales: {
        x: {
          grid: { display: false },
          ticks: {
            color: '#8d8da3',
            maxTicksLimit: 7,
          },
        },
        y: {
          beginAtZero: true,
          grid: { color: 'rgba(255, 255, 255, 0.08)' },
          ticks: {
            color: '#8d8da3',
            precision: 0,
          },
        },
      },
      plugins: {
        legend: { display: false },
      },
    });
  }

  private baseChartOptions(overrides: any = {}): any {
    const overridePlugins = overrides.plugins ?? {};

    return {
      locale: this.locale.locale(),
      responsive: true,
      maintainAspectRatio: false,
      interaction: {
        intersect: false,
        mode: 'index',
      },
      ...overrides,
      plugins: {
        tooltip: {
          backgroundColor: '#11121a',
          borderColor: 'rgba(255, 255, 255, 0.12)',
          borderWidth: 1,
          titleColor: '#ffffff',
          bodyColor: '#c7c7d4',
        },
        ...overridePlugins,
      },
    };
  }

  private destroyDashboardCharts(): void {
    this.charts.forEach((chart) => chart.destroy());
    this.charts = [];
  }
}
