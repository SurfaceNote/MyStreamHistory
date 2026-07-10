export interface ViewerStats {
    id: string;
    viewerId: string;
    streamerTwitchUserId: string;
    minutesWatched: number;
    earnedMsgPoints: number;
    experience: number;
    lastUpdatedAt: string;
    top100Rank?: number | null;
    streamsWatched: number;
    firstWatchedAt?: string | null;
    lastWatchedAt?: string | null;
    watchedPercentage: number;
    favoriteCategories: ViewerFavoriteCategory[];
    recentStreams: ViewerStreamHistory[];
    viewer?: ViewerInfo;
}

export interface ViewerFavoriteCategory {
    twitchCategoryId: string;
    name: string;
    boxArtUrl: string;
    minutesWatched: number;
    streamsCount: number;
}

export interface ViewerStreamHistory {
    streamSessionId: string;
    streamTitle?: string | null;
    startedAt: string;
    endedAt?: string | null;
    durationMinutes: number;
    minutesWatched: number;
    watchedPercentage: number;
    chatPoints: number;
    categories: string[];
}

export interface ViewerInfo {
    id: string;
    twitchUserId: string;
    displayName: string;
    login: string;
    profileImageUrl?: string;
    createdAt: string;
}







