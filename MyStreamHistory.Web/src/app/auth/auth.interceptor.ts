import { HttpEvent, HttpHandlerFn, HttpRequest } from "@angular/common/http";
import { inject } from "@angular/core";
import { catchError, Observable, switchMap, throwError } from "rxjs";
import { AuthService } from "./auth.service";

export function authIntercerptor(req: HttpRequest<unknown>, next: HttpHandlerFn): Observable<HttpEvent<unknown>>{
    const authService = inject(AuthService);
    const isAuthRequest = req.url.includes('/auth/twitch/callback') ||
                          req.url.includes('/auth/refresh-token');

    if (isAuthRequest) {
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
            if (error.status === 401 && !isAuthRequest) {
                return authService.refreshToken().pipe(
                    switchMap((newToken) => {
                        if (newToken) {
                            const retryReq = req.clone({
                                headers: req.headers.set('Authorization', `Bearer ${newToken}`),
                            });
                            return next(retryReq);
                        }
                        authService.logout();
                        return throwError(() => error);
                    }),
                    catchError(() => {
                        authService.logout();
                        return throwError(() => error);
                    })
                );
            }
            return throwError(() => error);
        })
    );
}
