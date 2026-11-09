/** Chat accounts exclude the broadcaster and bots. */
export interface StreamRecap {
  streamSessionId: string;
  isLive: boolean;
  viewerDataAvailable: boolean;
  previousStreamsCount: number;
  comparisonStreamsCount: number;
  durationMinutes: number;
  averageDurationMinutes: number | null;
  peakViewers: number | null;
  averageViewers: number | null;
  chatAccounts: number;
  averageChatAccounts: number | null;
  newCount: number;
  returningCount: number;
  newViewers: RecapViewer[];
  returningViewers: RecapViewer[];
  streakViewers: RecapViewer[];
  topChatters: RecapViewer[];
  playthroughs: RecapPlaythrough[];
}

export interface RecapViewer {
  twitchUserId: string;
  displayName: string;
  login: string;
  profileImageUrl: string | null;
  minutesWatched: number;
  chatPoints: number;
  missedStreams?: number | null;
  streak?: number | null;
}

export interface RecapPlaythrough {
  playthroughId: string;
  gameName: string;
  boxArtUrl: string;
  status: string;
  streamNumber: number;
  streamsCount: number;
}
