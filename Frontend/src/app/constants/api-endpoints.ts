import { environment } from "../../environments/environment";
import { GamesCatalogQuery } from "../models/game.model";

export const API_ENDPOINTS = {
    STREAMERS: {
        ALL: environment.api_url + '/user/all',
        NEW: environment.api_url+ '/user/get-new-users',
        POPULAR: environment.api_url + '/api/PopularStreamers',
        LIVE: environment.api_url + '/api/LiveStreamers'
    },
    STREAMER: (twitchId: number) => `${environment.api_url}/user/${twitchId}`,
    RECENT_STREAMS: (twitchId: number, count: number = 10, before?: string) =>
        `${environment.api_url}/user/${twitchId}/recent-streams?count=${count}` +
        (before ? `&before=${encodeURIComponent(before)}` : ''),
    TOP_VIEWERS: (twitchId: number, limit: number = 100, source?: 'overlay') =>
        `${environment.api_url}/user/${twitchId}/top-viewers?limit=${limit}` + (source ? `&source=${source}` : ''),
    VIEWER_STATS: (twitchId: number, viewerTwitchId: string) =>
        `${environment.api_url}/user/${twitchId}/viewers/${encodeURIComponent(viewerTwitchId)}`,
    SOCIAL_LINKS_PUBLIC: (twitchId: number) =>
        `${environment.api_url}/user/${twitchId}/social-links`,
    STREAMER_STATISTICS: (twitchId: number, period: string = '30d') =>
        `${environment.api_url}/user/${twitchId}/statistics?period=${period}`,
    STREAM_DETAILS: (streamId: string) => `${environment.api_url}/streams/${streamId}`,
    STREAM_RECAP: (streamId: string) => `${environment.api_url}/streams/${streamId}/recap`,
    GAME: (twitchId: string) => `${environment.api_url}/games/${encodeURIComponent(twitchId)}`,
    GAMES: (query: GamesCatalogQuery) => {
        const params = new URLSearchParams();
        if (query.search) params.set('search', query.search);
        if (query.lang) params.set('lang', query.lang);
        if (query.filter && query.filter !== 'all') params.set('filter', query.filter);
        if (query.sort && query.sort !== 'popular') params.set('sort', query.sort);
        if (query.page && query.page > 1) params.set('page', String(query.page));
        if (query.pageSize) params.set('pageSize', String(query.pageSize));
        const search = params.toString();
        return `${environment.api_url}/games` + (search ? `?${search}` : '');
    },
    SETTINGS: {
        SOCIAL_LINKS: `${environment.api_url}/settings/social-links`,
        PLAYTHROUGHS: `${environment.api_url}/settings/playthroughs`,
        COLLECTION_STATUS: `${environment.api_url}/settings/collection-status`
    },
    ACTIVITY_VISIT: `${environment.api_url}/activity/visit`
};
