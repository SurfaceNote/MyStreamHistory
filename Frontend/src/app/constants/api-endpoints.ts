import { environment } from "../../environments/environment";

export const API_ENDPOINTS = {
    STREAMERS: {
        NEW: environment.api_url+ '/user/get-new-users',
        POPULAR: environment.api_url + '/api/PopularStreamers',
        LIVE: environment.api_url + '/api/LiveStreamers'
    },
    STREAMER: environment.api_url + '/user/get-new-users'
};