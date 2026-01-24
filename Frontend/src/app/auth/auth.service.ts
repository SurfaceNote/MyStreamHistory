import { HttpClient } from "@angular/common/http";
import { inject, Injectable } from "@angular/core";
import { BehaviorSubject, Observable, of, throwError } from "rxjs";
import { catchError, filter, map, take, tap } from "rxjs/operators";
import { Router } from "@angular/router";
import { environment } from "../../environments/environment";
import { TokenResponse } from "../features/auth/models/token-response.model";
import { RefreshTokenRequest } from "../features/auth/models/refresh-token-request.model";
import { ApiResponse } from "../core/api/api-response.model";
import { unwrapData } from "../core/api/api-operators";

@Injectable({
    providedIn: 'root',
})
export class AuthService {
    private readonly apiUrl = environment.api_url;
    private readonly redirectUri = `${environment.url}/callback`;
    private readonly accessTokenKey = 'access_token';
    private readonly refreshTokenKey = 'refresh_token';
    private readonly clientId = environment.clientId;
    private isRefreshing = false;
    private refreshTokenSubject = new BehaviorSubject<string | null>(null);
    private tokenSubject = new BehaviorSubject<string | null>(this.getAccessToken());
    private usernameSubject = new BehaviorSubject<string | null>(this.getUsernameFromToken());

    private http = inject(HttpClient);
    private router = inject(Router);

    private setTokens(tokens: TokenResponse): void {
        localStorage.setItem(this.accessTokenKey, tokens.accessToken);
        localStorage.setItem(this.refreshTokenKey, tokens.refreshToken);

        this.tokenSubject.next(tokens.accessToken);
        this.usernameSubject.next(this.getUsernameFromToken());
    }

    private clearTokens(): void {
        localStorage.removeItem(this.accessTokenKey);
        localStorage.removeItem(this.refreshTokenKey);
        this.tokenSubject.next(null);
        this.usernameSubject.next(null);
    }

    generateRandomState(): string {
        const array = new Uint8Array(16);
        window.crypto.getRandomValues(array);
        return Array.from(array, byte => byte.toString(16).padStart(2, '0')).join('');
    }

    loginWithTwitch(): void {
        const scope = 'user:read:email+moderator:read:chatters';
        const state = this.generateRandomState();
        const twitchAuthUrl = `https://id.twitch.tv/oauth2/authorize?client_id=${this.clientId}&redirect_uri=${this.redirectUri}&response_type=code&scope=${scope}&state=${state}`;

        localStorage.setItem('twitchAuthState', state);
        window.location.href = twitchAuthUrl;
    }

    handleTwitchCallback(code: string, state: string): Observable<boolean> {
        const storedState = localStorage.getItem('twitchAuthState');
        if (state !== storedState) {
            console.error('Invalid state parameter');
            return of(false);
        }

        return this.http
            .get<ApiResponse<TokenResponse>>(`${this.apiUrl}/auth/twitch/callback`, { 
                params: { code, state }, 
                withCredentials: true
            })
            .pipe(
                unwrapData<TokenResponse>(),
                tap((tokens) => {
                    this.setTokens(tokens);
                    localStorage.removeItem('twitchAuthState');
                }),
                map(() => true),
                catchError((error) => {
                    console.error('Twitch callback failed', error);
                    return of(false);
                }
            )
        );
    }

    isLoggedIn(): boolean {
        return !!this.getAccessToken();
    }

    getAccessToken(): string | null {
        return localStorage.getItem(this.accessTokenKey);
    }

    getRefreshToken(): string | null {
        return localStorage.getItem(this.refreshTokenKey);
    }

    getAccessTokenObservable(): Observable<string | null> {
        return this.tokenSubject.asObservable();
    }

    getUsernameObservable(): Observable<string | null> {
        return this.usernameSubject.asObservable();
    }

    refreshToken(): Observable<string | null> {
        if (this.isRefreshing) {
            return this.refreshTokenSubject.pipe(
                filter((t): t is string => !!t),
                take(1)
            );
        }

        const accessToken = this.getAccessToken();
        const refreshToken = this.getRefreshToken();

        if (!accessToken || !refreshToken) {
            this.logoutLocal();
            return throwError(() => new Error('No tokens to refresh'));
        }

        this.isRefreshing = true;
        this.refreshTokenSubject.next(null);

        const body: RefreshTokenRequest = { accessToken, refreshToken };

        return this.http.
            post<ApiResponse<TokenResponse>>(`${this.apiUrl}/auth/refresh-token`, body, { withCredentials: true })
            .pipe(
                unwrapData<TokenResponse>(),
                tap((tokens) => {
                    this.setTokens(tokens);

                    this.refreshTokenSubject.next(tokens.accessToken);
                    this.isRefreshing = false;
                }),
                map((tokens) => tokens.accessToken),
                catchError((error) => {
                    console.error('Refreshing token failed', error);
                    this.isRefreshing = false;
                    this.logoutLocal();
                    return throwError(() => error);
                })
            );
    }

    getUsernameFromToken(): string | null {
        const token = this.getAccessToken();

        if (!token) {
            return null;
        }

        const payload = this.decodeJwtPayload(token);
        if (!payload) {
            return null;
        }

        return payload.UserName ?? payload.name ?? null;
    }
    
    getTwitchIdFromToken(): string | null {
        const token = this.getAccessToken();

        if (!token) {
            return null;
        }

        const payload = this.decodeJwtPayload(token);
        return payload?.TwitchId ?? null;
    }

    logoutLocal(): void {
        this.clearTokens();
        this.router.navigate(['/']);
    }

    private decodeJwtPayload(token: string): any | null {
        try {
            const base64Url = token.split('.')[1];
            const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
            const padded = base64 + '='.repeat((4 - (base64.length % 4)) % 4);
            return JSON.parse(decodeURIComponent(escape(atob(padded))));
        } catch {
            return null;
        }
    }

    logout(): void {
        this.http.post(`${this.apiUrl}/auth/logout`, {}, {withCredentials: true}).subscribe({
            next: () => {
                localStorage.removeItem(this.accessTokenKey);
                this.tokenSubject.next(null);
                this.usernameSubject.next(null);
                this.router.navigate(['/']);
            },
            error: (err) => {
                console.error('Logout failed', err);
                localStorage.removeItem(this.accessTokenKey);
                this.router.navigate(['/']);
            }
        })
    }

}
