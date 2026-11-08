export interface CollectionSubscription {
  eventType: string;
  status: string;
}

/** Owner-only view of whether MyStreamHistory is recording the channel. */
export interface CollectionStatus {
  dataCollectionEnabled: boolean;
  subscriptions: CollectionSubscription[];
  streamsCount: number;
  isLive: boolean;
  lastStreamStartedAt: string | null;
  lastStreamEndedAt: string | null;
  lastOverlaySeenAt: string | null;
  lastPanelSeenAt: string | null;
}
