export interface ViewerStats {
    id: string;
    viewerId: string;
    streamerTwitchUserId: string;
    minutesWatched: number;
    earnedMsgPoints: number;
    experience: number;
    lastUpdatedAt: string;
    viewer?: ViewerInfo;
}

export interface ViewerInfo {
    id: string;
    twitchUserId: string;
    displayName: string;
    login: string;
    profileImageUrl?: string;
    createdAt: string;
}

