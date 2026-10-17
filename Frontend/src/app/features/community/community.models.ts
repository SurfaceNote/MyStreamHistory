export type CommunityQuality = 'insufficient' | 'provisional' | 'verified';
export interface CommunityReport {
  methodVersion: number; calculatedAt: string | null; asOf: string | null; from: string | null;
  windowDays: number; timeZoneId: string; timeZoneConfigured: boolean; quality: CommunityQuality;
  recalculating: boolean; collectionEnabled: boolean; eligibleDays: number | null; unknownDays: number | null;
  coverage: number | null; core: number | null; previousCore: number | null;
  regularIncludingCore: number | null; firstObserved: number | null; declining: number | null;
  distribution: Record<string, number> | null;
  trend: { date: string; core: number | null; quality: CommunityQuality }[];
  transitions: Record<string, number>;
}
export interface CommunityMember {
  twitchUserId: string; displayName: string; login: string; profileImageUrl: string | null;
  level: string; quality: CommunityQuality; visitedDays: number; eligibleDays: number;
  regularity: number | null; messageCount: number | null; messagePoints?: number | null; daysWithMessages: number | null;
  hasHistoricalChat: boolean; messagesProvisional: boolean; firstObserved: string | null;
  consecutiveMisses: number; declining: boolean; dormant: boolean; returned?: boolean; transition: string | null;
  history: { date: string; status: string; quality: string }[];
}
export interface CommunityResponse {
  channelTwitchUserId?: string; canViewOtherChannels?: boolean; readOnly?: boolean;
  report: CommunityReport; members: CommunityMember[]; nextCursor: string | null;
  exclusions: { twitchUserId: string; displayName: string }[];
}
