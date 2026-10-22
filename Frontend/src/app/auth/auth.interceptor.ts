import { ANONYMOUS_REQUEST } from '../core/api/api-base';
import { isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID } from '@angular/core';
import { HttpEvent, HttpHandlerFn, HttpRequest } from "@angular/common/http";
import { inject } from "@angular/core";
import { catchError, Observable, switchMap, throwError } from "rxjs";
import { AuthService } from "./auth.service";

export function authIntercerptor(req: HttpRequest<unknown>, next: HttpHandlerFn): Observable<HttpEvent<unknown>>{
    if (!isPlatformBrowser(inject(PLATFORM_ID)) || req.context.get(ANONYMOUS_REQUEST)) {
        return next(req.clone({ withCredentials: false, headers: req.headers.delete('Authorization').delete('Cookie') }));
    }
    const authService = inject(AuthService);
    const isAuthRequest = req.url.includes('/auth/twitch/callback') ||
                          req.url.includes('/auth/refresh-token') ||
                          req.url.includes('/auth/logout');

    const skipTokenCompletely = req.url.includes('/auth/twitch/callback') ||
                                req.url.includes('/auth/refresh-token') ||
                                req.url.includes('/auth/logout');
    
    if (skipTokenCompletely) {
        return next(req);
    }

    const accessToken = authService.getAccessToken();
    let authReq = req;

    if (accessToken) {
        authReq = req.clone({
            headers: req.headers.set('Authorization', `Bearer ${accessToken}`),
        });
    }

    return next(authReq).pipe(
        catchError((error) => {
            const isRefreshRequest = req.url.includes('/auth/refresh-token');
            
            if (error.status === 401 && !isAuthRequest && !isRefreshRequest) {
                // Try to refresh the token
                return authService.refreshToken().pipe(
                    switchMap((newToken) => {
                        if (newToken) {
                            // Retry the original request with new token
                            const retryReq = req.clone({
                                headers: req.headers.set('Authorization', `Bearer ${newToken}`),
                            });
                            return next(retryReq);
                        }
                        // If refresh failed, logout
                        authService.logoutLocal();
                        return throwError(() => error);
                    }),
                    catchError((refreshError) => {
                        // If refresh token failed, just logout (no Twitch redirect)
                        authService.logoutLocal();
                        return throwError(() => refreshError);
                    })
                );
            }
            return throwError(() => error);
        })
    );
}
