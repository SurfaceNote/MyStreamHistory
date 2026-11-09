export type CommunityQuality = 'insufficient' | 'provisional' | 'verified';
export type CommunitySegment = 'returned' | 'new' | 'lost' | 'fading' | 'core' | 'regular' | 'occasional' | 'oneTime';
export type CommunityActivity = 'chatty' | 'silent';
export type SegmentCounts = Partial<Record<CommunitySegment, number>>;

export interface CommunityAccount { twitchUserId: string; displayName: string; }
export interface CommunityTrendPoint {
  date: string; segments: SegmentCounts; visitors: number; newViewers: number; returned: number;
  becameFading: number; becameLost: number; provisional: boolean;
}
export interface CommunityReport {
  methodVersion: number; calculatedAt: string | null; timeZoneId: string; timeZoneConfigured: boolean;
  quality: CommunityQuality; recalculating: boolean; collectionEnabled: boolean;
  windowSize: number; minBroadcasts: number; totalBroadcasts: number; skippedBroadcasts: number; coverage: number | null;
  windowDates: string[]; from: string | null; asOf: string | null;
  segments: SegmentCounts; previousSegments: SegmentCounts | null; previousDate: string | null;
  activity: Partial<Record<CommunityActivity, number>>;
  lastBroadcast: {
    date: string; visitors: number; newCount: number; returnedCount: number; missingCoreCount: number;
    new: CommunityAccount[]; returned: CommunityAccount[]; missingCore: CommunityAccount[];
  } | null;
  retention: { newcomers: number; cameBack: number; stayed: number } | null;
  trend: CommunityTrendPoint[];
}
export interface CommunityMember {
  twitchUserId: string; displayName: string; login: string; profileImageUrl: string | null;
  segment: CommunitySegment; activity: CommunityActivity | null;
  visitsInWindow: number; windowSize: number; totalVisits: number; missedStreak: number;
  messagesInWindow: number | null; minutesInWindow: number;
  firstSeen: string | null; lastSeen: string | null; attendance: string;
}
export interface CommunityMemberFilter { segment: CommunitySegment | ''; activity: CommunityActivity | ''; search: string; }
export interface CommunityResponse {
  channelTwitchUserId?: string; canViewOtherChannels?: boolean; readOnly?: boolean;
  report: CommunityReport; members: CommunityMember[]; membersTotal?: number | null; nextCursor: string | null;
  exclusions: { twitchUserId: string; displayName: string }[];
}
