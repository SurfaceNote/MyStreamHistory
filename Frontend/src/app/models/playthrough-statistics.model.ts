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
}
