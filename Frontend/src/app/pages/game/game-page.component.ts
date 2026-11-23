import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { GameArtComponent } from '../../components/game-art/game-art.component';
import { ResponseStatusService } from '../../core/api/response-status.service';
import { streamLanguageName } from '../../i18n/language-name';
import { LocaleService } from '../../i18n/locale.service';
import { LocalPathPipe, TranslatePipe } from '../../i18n/translate.pipe';
import { TranslationKey } from '../../i18n/translations';
import { GamePage, GameStreamer } from '../../models/game.model';
import { PlaythroughStatus } from '../../models/playthrough.model';
import { AnalyticsService } from '../../service/analytics.service';
import { GameService } from '../../service/game.service';
import { SeoService } from '../../service/seo.service';
import { GameLinkSource, gameIdFromSegment, gameSegment, gameSourceFromState } from './game-path';

export type GameStreamerSort = 'recent' | 'hours';

export interface LanguageOption {
  /** Language code, or '' for streamers whose language is not known. */
  code: string;
  label: string;
  count: number;
}

/** Below this many streamers a game page is too thin to be worth indexing. */
const MIN_INDEXED_STREAMERS = 2;

@Component({
  selector: 'app-game-page',
  imports: [TranslatePipe, LocalPathPipe, RouterLink, GameArtComponent],
  templateUrl: './game-page.component.html',
  styleUrl: './game-page.component.scss'
})
export class GamePageComponent implements OnInit, OnDestroy {
  readonly locale = inject(LocaleService);
  private readonly games = inject(GameService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly seo = inject(SeoService);
  private readonly responseStatus = inject(ResponseStatusService);
  private readonly analytics = inject(AnalyticsService);

  game: GamePage | null = null;
  isLoading = false;
  /** 'notFound' when nobody streamed the game, 'failed' when it could not be loaded. */
  error: 'notFound' | 'failed' | null = null;
  language: string | null = null;
  sort: GameStreamerSort = 'recent';

  private routeSub?: Subscription;
  private gameSub?: Subscription;

  readonly statusLabels: Record<PlaythroughStatus, TranslationKey> = {
    Playing: 'ui.playing', Completed: 'ui.completed', Planned: 'ui.planned', Dropped: 'ui.dropped'
  };

  ngOnInit(): void {
    this.routeSub = this.route.paramMap.subscribe(params => {
      const segment = params.get('gameId');
      const twitchId = gameIdFromSegment(segment);
      if (!twitchId) {
        this.showError('notFound', { status: 404 });
        return;
      }
      // A slug fix only changes the URL: keep the loaded game.
      if (this.game?.summary.twitchId === twitchId) {
        this.updateSeo(this.game);
        return;
      }
      // The first emission comes after the navigation that created the page has ended;
      // a later one (another game on the same page) comes while its navigation is still running.
      const navigation = this.router.getCurrentNavigation() ?? this.router.lastSuccessfulNavigation;
      const source = gameSourceFromState(navigation?.extras.state);
      this.load(twitchId, segment!, source);
    });
  }

  ngOnDestroy(): void {
    this.routeSub?.unsubscribe();
    this.gameSub?.unsubscribe();
  }

  private load(twitchId: string, segment: string, source: GameLinkSource | 'direct'): void {
    this.isLoading = true;
    this.error = null;
    this.game = null;
    this.language = null;
    this.gameSub?.unsubscribe();
    this.gameSub = this.games.getGamePage(twitchId).subscribe({
      next: game => {
        this.responseStatus.reset();
        this.game = game;
        this.isLoading = false;
        this.analytics.trackEvent('game_page_open', { source, is_game: game.summary.isGame, streamers: game.streamers.length });
        const canonical = gameSegment(game.summary.twitchId, game.summary.name);
        if (segment !== canonical) {
          // Old or hand-typed slug: show the page under its one canonical URL. The route's default
          // SEO is applied when that navigation ends, so the game's own tags go after it.
          void this.router.navigateByUrl(this.locale.path(['/game', canonical]), { replaceUrl: true })
            .then(() => this.updateSeo(game));
          return;
        }
        this.updateSeo(game);
      },
      error: err => this.showError(err?.status === 404 ? 'notFound' : 'failed', err)
    });
  }

  private showError(kind: 'notFound' | 'failed', err: { status?: number }): void {
    this.isLoading = false;
    this.game = null;
    this.error = kind;
    this.responseStatus.fail(err);
    this.seo.update({
      title: this.locale.t(kind === 'notFound' ? 'error.notFound' : 'error.unavailable'),
      description: this.locale.t(kind === 'notFound' ? 'game.notFound' : 'error.unavailable'),
      noIndex: true
    });
  }

  private updateSeo(game: GamePage): void {
    const { summary } = game;
    this.seo.update({
      title: this.locale.t('seo.gameTitle', { name: summary.name }),
      description: this.locale.t('seo.gameDescription', {
        name: summary.name,
        streamers: this.locale.plural('streamerCount', summary.channelsCount)
      }),
      image: this.boxArt(summary.boxArtUrl),
      noIndex: !summary.isGame || game.streamers.length < MIN_INDEXED_STREAMERS
    });
  }

  get liveStreamers(): GameStreamer[] {
    return this.game?.streamers.filter(s => s.isLiveWithGame) ?? [];
  }

  /** Language buttons: only the languages the game's streamers actually use. */
  get languageOptions(): LanguageOption[] {
    const counts = new Map<string, number>();
    for (const streamer of this.game?.streamers ?? []) {
      const code = streamer.language ?? '';
      counts.set(code, (counts.get(code) ?? 0) + 1);
    }
    return [...counts]
      .map(([code, count]) => ({ code, count, label: this.languageName(code) }))
      .sort((a, b) => b.count - a.count || a.label.localeCompare(b.label, this.locale.locale()));
  }

  get visibleStreamers(): GameStreamer[] {
    const streamers = (this.game?.streamers ?? [])
      .filter(s => this.language === null || (s.language ?? '') === this.language);
    return this.sort === 'hours'
      ? [...streamers].sort((a, b) => b.hours - a.hours || this.byRecent(a, b))
      : [...streamers].sort((a, b) => this.byRecent(a, b));
  }

  /** The playthrough column only makes sense when someone tracks one. */
  get hasPlaythroughs(): boolean {
    return !!this.game?.streamers.some(s => s.playthrough);
  }

  selectLanguage(code: string | null): void {
    this.language = code;
  }

  selectSort(sort: GameStreamerSort): void {
    this.sort = sort;
  }

  languageName(code: string | null | undefined): string {
    if (!code) return this.locale.t('game.languageUnknown');
    if (code === 'other') return this.locale.t('game.languageOther');
    return streamLanguageName(code, this.locale.locale()) ?? code.toUpperCase();
  }

  hours(value: number): string {
    return this.locale.t('game.hours', { value: this.locale.number(value, 1) });
  }

  date(value: string): string {
    return new Date(value).toLocaleDateString(this.locale.locale(), {
      timeZone: 'UTC', day: 'numeric', month: 'short', year: 'numeric'
    });
  }

  boxArt(url: string): string {
    return url.replace(/\{width\}/g, '288').replace(/\{height\}/g, '384');
  }

  initials(name: string): string {
    return name.replace(/[^\p{L}\p{N}]/gu, '').slice(0, 2).toUpperCase() || '?';
  }

  twitchUrl(login: string): string {
    return `https://www.twitch.tv/${encodeURIComponent(login)}`;
  }

  // Live first: the server orders that way, and a viewer looking for a stream wants it on top.
  private byRecent(a: GameStreamer, b: GameStreamer): number {
    return Number(b.isLiveWithGame) - Number(a.isLiveWithGame)
      || Date.parse(b.lastStreamedAt) - Date.parse(a.lastStreamedAt);
  }
}
