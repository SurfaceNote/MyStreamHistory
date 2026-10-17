export interface StreamDetails {
  id: string;
  streamId: string | null;
  twitchUserId: number;
  streamerLogin: string;
  streamerDisplayName: string;
  streamerAvatarUrl: string | null;
  startedAt: Date;
  endedAt: Date | null;
  isLive: boolean;
  streamTitle: string | null;
  gameName: string | null;
  viewerCount: number | null;
  audience?: AudiencePoint[];
  categories: CategoryDetails[];
  viewers: StreamViewer[];
  viewerDataAvailable?: boolean;
}

export interface AudiencePoint {
  startedAt: string;
  averageViewers: number | null;
  sampleCount: number;
}

export interface CategoryDetails {
  streamCategoryId: string;
  twitchId: string;
  name: string;
  boxArtUrl: string;
  startedAt: Date;
  endedAt: Date | null;
  durationMinutes: number;
  uniqueViewers: number | null;
}

export interface StreamViewer {
  twitchUserId: string;
  displayName: string;
  login: string;
  profileImageUrl: string | null;
  minutesWatched: number;
  chatPoints: number;
  viewerPoints: number;
}

