/** Owner-only view of whether MyStreamHistory is recording the channel. */
export interface CollectionStatus {
  dataCollectionEnabled: boolean;
  streamsCount: number;
  isLive: boolean;
  lastStreamStartedAt: string | null;
  lastStreamEndedAt: string | null;
  lastOverlaySeenAt: string | null;
  lastPanelSeenAt: string | null;
}
