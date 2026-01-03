import { inject } from "@angular/core"
import { AuthService } from "./auth.service"
import { CanActivateFn, Router } from "@angular/router";
import { map, tap } from "rxjs";

export const authGuard: CanActivateFn = () => {
    const authService = inject(AuthService);
    const router = inject(Router);

    if (authService.isLoggedIn()) {
        return true;
    }

    return authService.refreshToken().pipe(
        tap((token) => {
            if (!token) {
                router.navigate(['/']);
            }
        }),
        map((token) => !!token)
    );
}
