import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_ENDPOINTS } from '../constants/api-endpoints';
import { GetSocialLinksResponse, UpdateSocialLinksRequest, UpdateSocialLinksResponse } from '../models/social-link.model';
import { Playthrough, PlaythroughSettings, UpsertPlaythroughRequest } from '../models/playthrough.model';
import { ApiResponse } from '../core/api/api-response.model';
import { CollectionStatus } from '../models/collection-status.model';
import { unwrapData } from '../core/api/api-operators';

@Injectable({
  providedIn: 'root'
})
export class SettingsService {
  constructor(private http: HttpClient) {}

  getSocialLinks(): Observable<GetSocialLinksResponse> {
    return this.http.get<ApiResponse<GetSocialLinksResponse>>(API_ENDPOINTS.SETTINGS.SOCIAL_LINKS)
      .pipe(
        unwrapData<GetSocialLinksResponse>()
      );
  }

  updateSocialLinks(request: UpdateSocialLinksRequest): Observable<UpdateSocialLinksResponse> {
    return this.http.patch<ApiResponse<UpdateSocialLinksResponse>>(
      API_ENDPOINTS.SETTINGS.SOCIAL_LINKS,
      request
    ).pipe(
      unwrapData<UpdateSocialLinksResponse>()
    );
  }

  getCollectionStatus(): Observable<CollectionStatus> {
    return this.http.get<ApiResponse<CollectionStatus>>(API_ENDPOINTS.SETTINGS.COLLECTION_STATUS)
      .pipe(unwrapData<CollectionStatus>());
  }

  getPlaythroughs(): Observable<PlaythroughSettings> {
    return this.http.get<ApiResponse<PlaythroughSettings>>(API_ENDPOINTS.SETTINGS.PLAYTHROUGHS)
      .pipe(
        unwrapData<PlaythroughSettings>()
      );
  }

  savePlaythrough(request: UpsertPlaythroughRequest): Observable<Playthrough> {
    return this.http.post<ApiResponse<Playthrough>>(
      API_ENDPOINTS.SETTINGS.PLAYTHROUGHS,
      request
    ).pipe(
      unwrapData<Playthrough>()
    );
  }

  deletePlaythrough(playthroughId: string): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${API_ENDPOINTS.SETTINGS.PLAYTHROUGHS}/${playthroughId}`)
      .pipe(
        unwrapData<void>()
      );
  }
}

