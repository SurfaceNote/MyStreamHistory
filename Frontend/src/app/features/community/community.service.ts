import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/api/api-response.model';
import { unwrapData } from '../../core/api/api-operators';
import { CommunityResponse } from './community.models';

@Injectable({ providedIn: 'root' })
export class CommunityService {
  private http = inject(HttpClient);
  private url = `${environment.api_url}/analytics/community`;
  private channelParams(channelId?: string | null) { return channelId ? new HttpParams().set('channelId', channelId) : new HttpParams(); }
  report(channelId?: string | null) { return this.http.get<ApiResponse<CommunityResponse>>(this.url, { params: this.channelParams(channelId) }).pipe(unwrapData<CommunityResponse>()); }
  members(level: string, cursor: string | null = null, channelId?: string | null) {
    let params = this.channelParams(channelId);
    if (level) params = params.set('level', level);
    if (cursor) params = params.set('cursor', cursor);
    return this.http.get<ApiResponse<CommunityResponse>>(`${this.url}/members`, { params }).pipe(unwrapData<CommunityResponse>());
  }
  settings(timeZoneId: string) {
    return this.http.put<ApiResponse<CommunityResponse>>(`${this.url}/settings`, { timeZoneId }).pipe(unwrapData<CommunityResponse>());
  }
  exclusions(channelId?: string | null) { return this.http.get<ApiResponse<CommunityResponse>>(`${this.url}/exclusions`, { params: this.channelParams(channelId) }).pipe(unwrapData<CommunityResponse>()); }
  exclude(id: string) {
    return this.http.put<ApiResponse<CommunityResponse>>(`${this.url}/exclusions/${encodeURIComponent(id)}`, {}).pipe(unwrapData<CommunityResponse>());
  }
  restore(id: string) {
    return this.http.delete<ApiResponse<CommunityResponse>>(`${this.url}/exclusions/${encodeURIComponent(id)}`).pipe(unwrapData<CommunityResponse>());
  }
}
