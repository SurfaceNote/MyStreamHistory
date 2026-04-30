import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiResponse } from '../core/api/api-response.model';
import { unwrapData } from '../core/api/api-operators';
import {
  ChatSubscriptionsResponse,
  CleanupChatSubscriptionsResponse,
  DeleteSubscriptionsResponse,
  EventSubSubscriptions,
  SubscribeToAllUsersResponse
} from '../models/diagnostics.model';

@Injectable({
  providedIn: 'root'
})
export class DiagnosticsService {
  private readonly baseUrl = `${environment.api_url}/diagnostics`;

  constructor(private http: HttpClient) {}

  getEventSubSubscriptions(): Observable<EventSubSubscriptions> {
    return this.http
      .get<ApiResponse<EventSubSubscriptions>>(`${this.baseUrl}/eventsub-subscriptions`)
      .pipe(unwrapData<EventSubSubscriptions>());
  }

  deleteAllEventSubSubscriptions(): Observable<DeleteSubscriptionsResponse> {
    return this.http
      .delete<ApiResponse<DeleteSubscriptionsResponse>>(
        `${this.baseUrl}/eventsub-subscriptions`,
        {
          headers: this.confirmationHeaders('delete-all-eventsub-subscriptions')
        }
      )
      .pipe(unwrapData<DeleteSubscriptionsResponse>());
  }

  subscribeToAllUsers(): Observable<SubscribeToAllUsersResponse> {
    return this.http
      .post<ApiResponse<SubscribeToAllUsersResponse>>(
        `${this.baseUrl}/eventsub-subscriptions/subscribe-all`,
        {}
      )
      .pipe(unwrapData<SubscribeToAllUsersResponse>());
  }

  getChatSubscriptions(): Observable<ChatSubscriptionsResponse> {
    return this.http
      .get<ApiResponse<ChatSubscriptionsResponse>>(`${this.baseUrl}/chat-subscriptions`)
      .pipe(unwrapData<ChatSubscriptionsResponse>());
  }

  cleanupChatSubscriptions(): Observable<CleanupChatSubscriptionsResponse> {
    return this.http
      .delete<ApiResponse<CleanupChatSubscriptionsResponse>>(
        `${this.baseUrl}/chat-subscriptions`,
        {
          headers: this.confirmationHeaders('cleanup-all-chat-subscriptions')
        }
      )
      .pipe(unwrapData<CleanupChatSubscriptionsResponse>());
  }

  private confirmationHeaders(value: string): HttpHeaders {
    return new HttpHeaders({
      'X-Diagnostics-Confirmation': value,
      'X-Correlation-ID': crypto.randomUUID()
    });
  }
}
