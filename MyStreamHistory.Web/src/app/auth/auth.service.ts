import { HttpClient } from "@angular/common/http";
import { inject, Injectable } from "@angular/core";
import { BehaviorSubject, Observable, of, throwError } from "rxjs";
import { catchError, map, tap } from "rxjs/operators";
import { Router } from "@angular/router";
import { environment } from "../../environments/environment";
import { TokenResponse } from "./auth.model";

@Injectable({
    providedIn: 'root',
})
export class AuthService {
    private readonly apiUrl = environment.api_url;
    private readonly redirectUri = `${environment.url}/callback`;
    private readonly accessTokenKey = 'access_token';
    private readonly clientId = environment.clientId;
    private isRefreshing = false;
    private refreshTokenSubject = new BehaviorSubject<string | null>(null);
    private tokenSubject = new BehaviorSubject<string | null>(this.getAccessToken());
    private usernameSubject = new BehaviorSubject<string | null>(this.getUsernameFromToken());

    private http = inject(HttpClient);
    private router = inject(Router);

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
            .get<TokenResponse>(`${this.apiUrl}/auth/twitch/callback`, { params: { code, state }, withCredentials: true})
            .pipe(
                tap((response) => {
                    localStorage.setItem(this.accessTokenKey, response.AccessToken);
                    this.tokenSubject.next(response.AccessToken);
                    this.usernameSubject.next(this.getUsernameFromToken());
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

    getAccessTokenObservable(): Observable<string | null> {
        return this.tokenSubject.asObservable();
    }

    getUsernameObservable(): Observable<string | null> {
        return this.usernameSubject.asObservable();
    }

    refreshToken(): Observable<string | null> {
        if (this.isRefreshing) {
            return this.refreshTokenSubject.asObservable().pipe(
                map((token) => token || this.getAccessToken())
            );
        }

        this.isRefreshing = true;
        this.refreshTokenSubject.next(null);

        return this.http.post<TokenResponse>(`${this.apiUrl}/auth/refresh-token`, {}, {withCredentials: true}).pipe(
            tap((response) => {
                localStorage.setItem(this.accessTokenKey, response.AccessToken);
                this.refreshTokenSubject.next(response.AccessToken);
                this.isRefreshing = false;
            }),
            map((response) => response.AccessToken),
            catchError((error) => {
                console.error('Refresh token failed', error);
                this.isRefreshing = false;
                this.logout();
                return throwError(() => new Error('Refresh token failed'));
            })
        );
    }

    getUsernameFromToken(): string | null {
        const token = this.getAccessToken();

        if (!token) {
            return null;
        }

        try {
            const payload = JSON.parse(atob(token.split('.')[1]));
            return payload.name;
        } catch(e) {
            console.error('Failed to decode JWT token', e);
            return null;
        }
    }

    logout(): void {
        localStorage.removeItem(this.accessTokenKey);
        this.tokenSubject.next(null);
        this.usernameSubject.next(null);
        this.router.navigate(['/']);
    }

}
