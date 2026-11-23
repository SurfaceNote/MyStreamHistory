import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE, ANONYMOUS_REQUEST } from '../core/api/api-base';
import { ApiResponse } from '../core/api/api-response.model';
import { unwrapData } from '../core/api/api-operators';
import { API_ENDPOINTS } from '../constants/api-endpoints';
import { GamePage, GamesCatalog, GamesCatalogQuery } from '../models/game.model';

@Injectable({ providedIn: 'root' })
export class GameService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE);

  getGamePage(twitchId: string): Observable<GamePage> {
    return this.http.get<ApiResponse<GamePage>>(this.endpoint(API_ENDPOINTS.GAME(twitchId)), {
      context: new HttpContext().set(ANONYMOUS_REQUEST, true)
    }).pipe(unwrapData<GamePage>());
  }

  getCatalog(query: GamesCatalogQuery): Observable<GamesCatalog> {
    return this.http.get<ApiResponse<GamesCatalog>>(this.endpoint(API_ENDPOINTS.GAMES(query)), {
      context: new HttpContext().set(ANONYMOUS_REQUEST, true)
    }).pipe(unwrapData<GamesCatalog>());
  }

  private endpoint(url: string): string {
    const parsed = new URL(url, 'https://api.invalid');
    return this.base.replace(/\/$/, '') + parsed.pathname + parsed.search;
  }
}
