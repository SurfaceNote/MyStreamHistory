import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { API_ENDPOINTS } from '../constants/api-endpoints';
import { GetSocialLinksResponse, UpdateSocialLinksRequest, UpdateSocialLinksResponse } from '../models/social-link.model';
import { ApiResponse } from '../core/api/api-response.model';
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
    return this.http.put<ApiResponse<UpdateSocialLinksResponse>>(
      API_ENDPOINTS.SETTINGS.SOCIAL_LINKS,
      request
    ).pipe(
      unwrapData<UpdateSocialLinksResponse>()
    );
  }
}

