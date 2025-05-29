import { inject } from "@angular/core";
import { AuthService } from "../auth/auth.service";
import { tap } from "rxjs";

export function appInitializer() {
    const authService = inject(AuthService);

    return () => {
        if (authService.isLoggedIn()) {
            return authService.refreshToken().pipe(
                tap((token) => {
                    if (!token) {
                        authService.logout();
                    }
                })
            );
        }
        return Promise.resolve();
    }
}
