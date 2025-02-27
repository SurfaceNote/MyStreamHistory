import { Injectable } from "@angular/core";
import { HttpClient } from '@angular/common/http';
import { Observable } from "rxjs";
import { StreamerShortDTO } from "../models/streamer-short.dto";
import { API_ENDPOINTS } from "../constants/api-endpoints";
import { StreamerListType } from "../enums/streamer-list-type.enum";

@Injectable({
    providedIn: 'root'
})
export class StreamerService {
    constructor(private http: HttpClient) {}

    getStreamers(streamersListType: StreamerListType): Observable<StreamerShortDTO[]> {
        let url: string;

        switch(streamersListType) {
            case StreamerListType.NewStreamers:
                url = API_ENDPOINTS.STREAMERS.NEW;
                break;
            default:
                throw new Error('Unkown stream list type');
        }

        return this.http.get<StreamerShortDTO[]>(url);
    }
};