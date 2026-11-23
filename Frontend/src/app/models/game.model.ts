import { PlaythroughStatus } from './playthrough.model';

/** Statistics of one Twitch category across every streamer on the site (`GET /games/{twitchId}`). */
export interface GameSummary {
  twitchId: string;
  name: string;
  boxArtUrl: string;
  igdbId?: string | null;
  /** False for Twitch categories that are not games (Just Chatting, Music, …). */
  isGame: boolean;
  totalHours: number;
  hoursLast30Days: number;
  streamsCount: number;
  channelsCount: number;
  /** Main languages of the game's streamers, most common first. */
  languages: string[];
  liveCount: number;
  completedCount: number;
  firstStreamedAt: string;
  lastStreamedAt: string;
}

export interface GamePlaythrough {
  status: PlaythroughStatus;
  title: string;
  streamsCount: number;
}

export interface GameStreamer {
  twitchUserId: number;
  streamerLogin: string;
  streamerDisplayName: string;
  streamerAvatarUrl?: string | null;
  /** Most common language of the streamer's streams with this game. */
  language?: string | null;
  hours: number;
  streamsCount: number;
  firstStreamedAt: string;
  lastStreamedAt: string;
  isLiveWithGame: boolean;
  playthrough?: GamePlaythrough | null;
}

export type GamesCatalogFilter = 'all' | 'live' | 'completed';
export type GamesCatalogSort = 'popular' | 'recent' | 'hours';

export interface GamesCatalogQuery {
  search?: string | null;
  lang?: string | null;
  filter?: GamesCatalogFilter;
  sort?: GamesCatalogSort;
  page?: number;
  pageSize?: number;
}

/** `GET /games`: one page of games streamed on the site; non-game categories are left out. */
export interface GamesCatalog {
  page: number;
  pageSize: number;
  /** Games matching the query, across all pages. */
  totalCount: number;
  /** Games in the whole catalog, before search and filters. */
  catalogCount: number;
  /** Languages of the whole catalog with their number of games, most common first. */
  languages: { language: string; gamesCount: number }[];
  items: GameSummary[];
}

export interface GamePage {
  summary: GameSummary;
  /** Live with the game first, then by the latest stream. */
  streamers: GameStreamer[];
}
