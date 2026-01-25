import { TwitchCategory } from './twitch-category.model';

export interface StreamSession {
    id: string;
    streamId?: string;
    twitchUserId: number;
    streamerLogin: string;
    streamerDisplayName: string;
    startedAt: string;
    endedAt?: string;
    isLive: boolean;
    streamTitle?: string;
    gameName?: string;
    viewerCount?: number;
    categories: TwitchCategory[];
}

