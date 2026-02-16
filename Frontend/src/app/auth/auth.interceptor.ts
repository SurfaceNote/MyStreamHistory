import { HttpEvent, HttpHandlerFn, HttpRequest } from "@angular/common/http";
import { inject } from "@angular/core";
import { catchError, Observable, switchMap, throwError } from "rxjs";
import { AuthService } from "./auth.service";

export function authIntercerptor(req: HttpRequest<unknown>, next: HttpHandlerFn): Observable<HttpEvent<unknown>>{
    const authService = inject(AuthService);
    const isAuthRequest = req.url.includes('/auth/twitch/callback') ||
                          req.url.includes('/auth/refresh-token');

    // For refresh-token, we still need to add the Authorization header (with expired token)
    // because Gateway extracts UserId from it
    const skipTokenCompletely = req.url.includes('/auth/twitch/callback');
    
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
