export interface PlaythroughStatistics {
  playthroughId: string;
  title: string;
  status: string;
  twitchCategoryId: string;
  twitchCategoryTwitchId: string;
  gameName: string;
  boxArtUrl: string;
  firstStreamStartedAt?: string | null;
  lastStreamStartedAt?: string | null;
  uniqueViewersCount: number;
  totalHours: number;
  /** Distinct streams in the playthrough. */
  streamsCount?: number;
  /** 1 for the first playthrough of a game, 2 for a replay, and so on. */
  runNumber?: number;
  runsCount?: number;
}
