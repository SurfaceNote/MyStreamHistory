import { environment } from "../../environments/environment";

export const API_ENDPOINTS = {
    STREAMERS: {
        NEW: environment.api_url+ '/user/get-new-users',
        POPULAR: environment.api_url + '/api/PopularStreamers',
        LIVE: environment.api_url + '/api/LiveStreamers'
    },
    STREAMER: (twitchId: number) => `${environment.api_url}/user/${twitchId}`,
    RECENT_STREAMS: (twitchId: number, count: number = 10) => 
        `${environment.api_url}/user/${twitchId}/recent-streams?count=${count}`,
    TOP_VIEWERS: (twitchId: number, limit: number = 100) =>
        `${environment.api_url}/user/${twitchId}/top-viewers?limit=${limit}`,
    STREAM_DETAILS: (streamId: string) => `${environment.api_url}/streams/${streamId}`
};