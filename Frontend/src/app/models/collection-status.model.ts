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

export type TrackingState = 'active' | 'pending' | 'missing' | 'paused';

const REQUIRED_EVENTS = ['stream.online', 'stream.offline'];

export function trackingState(status: CollectionStatus): TrackingState {
  if (!status.dataCollectionEnabled) return 'paused';
  const byEvent = new Map(status.subscriptions.map(subscription => [subscription.eventType, subscription.status]));
  if (REQUIRED_EVENTS.some(event => !byEvent.has(event))) return 'missing';
  if (REQUIRED_EVENTS.every(event => byEvent.get(event) === 'enabled')) return 'active';
  if (REQUIRED_EVENTS.some(event => (byEvent.get(event) ?? '').includes('pending'))) return 'pending';
  return 'missing';
}
