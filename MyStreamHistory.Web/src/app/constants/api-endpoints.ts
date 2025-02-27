import { environment } from "../../environments/environment";

export const API_ENDPOINTS = {
    STREAMERS: {
        NEW: environment.api_url+ '/api/Streamer',
        POPULAR: environment.api_url + '/api/PopularStreamers',
        LIVE: environment.api_url + '/api/LiveStreamers'
    }
};