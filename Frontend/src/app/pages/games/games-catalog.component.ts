import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { Subject, Subscription, debounceTime, distinctUntilChanged } from 'rxjs';
import { GameArtComponent } from '../../components/game-art/game-art.component';
import { ResponseStatusService } from '../../core/api/response-status.service';
import { streamLanguageName } from '../../i18n/language-name';
import { LocaleService } from '../../i18n/locale.service';
import { LocalPathPipe, TranslatePipe } from '../../i18n/translate.pipe';
import { GameSummary, GamesCatalog, GamesCatalogFilter, GamesCatalogQuery, GamesCatalogSort } from '../../models/game.model';
import { AnalyticsService } from '../../service/analytics.service';
import { GameService } from '../../service/game.service';
import { SeoService } from '../../service/seo.service';
import { gamePath } from '../game/game-path';

export const GAMES_PAGE_SIZE = 48;

/** Catalog control reported with the `games_catalog_filter` analytics event. */
export type GamesCatalogControl = 'search' | 'filter' | 'language' | 'sort' | 'reset';

const FILTERS: readonly GamesCatalogFilter[] = ['all', 'live', 'completed'];
const SORTS: readonly GamesCatalogSort[] = ['popular', 'recent', 'hours'];

/** Catalog state as it lives in the URL: `/games?q=…&lang=…&filter=…&sort=…`. */
export interface GamesCatalogState {
  search: string;
  lang: string | null;
  filter: GamesCatalogFilter;
  sort: GamesCatalogSort;
}

/** Reads the URL; unknown values fall back to the defaults instead of breaking a shared link. */
export function catalogStateFromParams(params: ParamMap): GamesCatalogState {
  const filter = params.get('filter') as GamesCatalogFilter;
  const sort = params.get('sort') as GamesCatalogSort;
  const lang = params.get('lang')?.trim().toLowerCase() ?? '';
  return {
    search: (params.get('q') ?? '').trim().slice(0, 100),
    lang: /^([a-z]{2,3}(-[a-z]{2,4})?|other)$/.test(lang) ? lang : null,
    filter: FILTERS.includes(filter) ? filter : 'all',
    sort: SORTS.includes(sort) ? sort : 'popular'
  };
}

/** Query parameters for a state; defaults are left out so the plain catalog stays at `/games`. */
export function catalogQueryParams(state: GamesCatalogState): Record<string, string | null> {
  return {
    q: state.search || null,
    lang: state.lang,
    filter: state.filter === 'all' ? null : state.filter,
    sort: state.sort === 'popular' ? null : state.sort
  };
}

@Component({
  selector: 'app-games-catalog',
  imports: [TranslatePipe, LocalPathPipe, RouterLink, GameArtComponent],
  templateUrl: './games-catalog.component.html',
  styleUrl: './games-catalog.component.scss'
})
export class GamesCatalogComponent implements OnInit, OnDestroy {
  readonly locale = inject(LocaleService);
  private readonly games = inject(GameService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly seo = inject(SeoService);
  private readonly responseStatus = inject(ResponseStatusService);
  private readonly analytics = inject(AnalyticsService);

  readonly filters = FILTERS;
  readonly sorts = SORTS;
  state: GamesCatalogState = { search: '', lang: null, filter: 'all', sort: 'popular' };
  /** What the search box shows; it reaches the URL after a short pause. */
  searchText = '';
  items: GameSummary[] = [];
  catalog: GamesCatalog | null = null;
  loading = true;
  loadingMore = false;
  failed = false;

  private readonly searchInput = new Subject<string>();
  private subscriptions = new Subscription();
  private request?: Subscription;

  ngOnInit(): void {
    this.subscriptions.add(this.route.queryParamMap.subscribe(params => {
      this.state = catalogStateFromParams(params);
      this.searchText = this.state.search;
      this.load(1);
    }));
    this.subscriptions.add(this.searchInput
      .pipe(debounceTime(300), distinctUntilChanged())
      .subscribe(search => this.navigate({ search: search.trim() }, 'search')));
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    this.request?.unsubscribe();
  }

  onSearch(value: string): void {
    this.searchText = value;
    this.searchInput.next(value);
  }

  setFilter(filter: GamesCatalogFilter): void { this.navigate({ filter }, 'filter'); }
  setLanguage(lang: string | null): void { this.navigate({ lang }, 'language'); }
  setSort(sort: GamesCatalogSort): void { this.navigate({ sort }, 'sort'); }

  resetFilters(): void {
    this.searchText = '';
    this.navigate({ search: '', lang: null, filter: 'all' }, 'reset');
  }

  loadMore(): void {
    if (this.catalog && this.hasMore) this.load(this.catalog.page + 1);
  }

  get hasMore(): boolean {
    return !!this.catalog && this.items.length < this.catalog.totalCount;
  }

  get hasFilters(): boolean {
    return !!this.state.search || !!this.state.lang || this.state.filter !== 'all';
  }

  path(game: GameSummary): string {
    return gamePath(game.twitchId, game.name);
  }

  languageName(code: string): string {
    return code === 'other' ? this.locale.t('game.languageOther') : streamLanguageName(code, this.locale.locale()) ?? code.toUpperCase();
  }

  hours(value: number): string {
    return this.locale.t('game.hours', { value: this.locale.number(value, value >= 100 ? 0 : 1) });
  }

  readonly filterLabels = { all: 'ui.all', live: 'games.filterLive', completed: 'games.filterCompleted' } as const;
  readonly sortLabels = { popular: 'games.sortPopular', recent: 'games.sortRecent', hours: 'games.sortHours' } as const;

  private navigate(change: Partial<GamesCatalogState>, control: GamesCatalogControl): void {
    const next = { ...this.state, ...change };
    const changed = (Object.keys(change) as (keyof GamesCatalogState)[]).some(key => next[key] !== this.state[key]);
    if (!changed) return;
    // The search text itself stays out of analytics: only whether a search is set. The parameter is not
    // called `value`: GA4 reads `value` as the event's numeric value.
    const option = control === 'search' ? (next.search ? 'text' : 'cleared')
      : control === 'language' ? next.lang ?? 'all'
      : control === 'reset' ? 'all'
      : String(next[control]);
    this.analytics.trackEvent('games_catalog_filter', { control, option });
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: catalogQueryParams(next),
      replaceUrl: change.search !== undefined && Object.keys(change).length === 1
    });
  }

  private load(page: number): void {
    const query: GamesCatalogQuery = {
      search: this.state.search, lang: this.state.lang, filter: this.state.filter, sort: this.state.sort,
      page, pageSize: GAMES_PAGE_SIZE
    };
    this.failed = false;
    if (page === 1) {
      this.loading = true;
    } else {
      this.loadingMore = true;
    }
    this.request?.unsubscribe();
    this.request = this.games.getCatalog(query).subscribe({
      next: catalog => {
        this.responseStatus.reset();
        this.catalog = catalog;
        this.items = page === 1 ? catalog.items : [...this.items, ...catalog.items];
        this.loading = this.loadingMore = false;
        this.seo.update({
          title: this.locale.t('seo.gamesTitle'),
          description: this.locale.t('seo.gamesDescription', { count: catalog.catalogCount })
        });
      },
      error: err => {
        this.loading = this.loadingMore = false;
        this.failed = true;
        if (page === 1) {
          this.items = [];
          this.responseStatus.fail(err?.status ? err : { status: 502 });
          this.seo.update({ title: this.locale.t('error.unavailable'), description: this.locale.t('error.unavailable'), noIndex: true });
        }
      }
    });
  }
}
