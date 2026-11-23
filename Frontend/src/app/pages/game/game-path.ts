import { articleSlug } from '../../features/content/article-slug';

/** URL segment of a game page: `{twitchId}-{slug}`, or just the id when the name has nothing to slug. */
export function gameSegment(twitchId: string, name: string): string {
  const slug = articleSlug(name);
  return slug ? `${twitchId}-${slug}` : twitchId;
}

/** Unlocalized path of a game page; pass it through `LocaleService.path` or the `localPath` pipe. */
export function gamePath(twitchId: string, name: string): string {
  return `/game/${gameSegment(twitchId, name)}`;
}

/** Twitch category id from a `/game/:gameId` segment; null when it does not start with one. */
export function gameIdFromSegment(segment: string | null | undefined): string | null {
  return /^(\d{1,20})(?:-|$)/.exec(segment ?? '')?.[1] ?? null;
}

/** Where a visitor opened a game page from; reported with the `game_page_open` analytics event. */
export type GameLinkSource =
  | 'catalog' | 'profile_games' | 'profile_top_games' | 'profile_playthroughs' | 'stream_page' | 'viewer_stats';

const SOURCES: readonly GameLinkSource[] =
  ['catalog', 'profile_games', 'profile_top_games', 'profile_playthroughs', 'stream_page', 'viewer_stats'];

/** Navigation state for a link to a game page; it keeps the source out of the URL. */
export function gameLinkState(source: GameLinkSource): { gameSource: GameLinkSource } {
  return { gameSource: source };
}

/** Source from navigation state; 'direct' for a typed URL, a bookmark, a search result or a reload. */
export function gameSourceFromState(state: unknown): GameLinkSource | 'direct' {
  const source = (state as { gameSource?: unknown } | null | undefined)?.gameSource;
  return SOURCES.includes(source as GameLinkSource) ? source as GameLinkSource : 'direct';
}
