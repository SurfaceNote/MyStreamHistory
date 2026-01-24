export interface StreamSession {
    id: string;
    twitchUserId: number;
    streamerLogin: string;
    streamerDisplayName: string;
    startedAt: string;
    endedAt?: string;
    isLive: boolean;
    streamTitle?: string;
    gameName?: string;
    viewerCount?: number;
}

