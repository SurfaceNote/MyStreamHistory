import { LocaleService } from '../i18n/locale.service';
import { inject } from "@angular/core"
import { AuthService } from "./auth.service"
import { CanActivateFn, Router } from "@angular/router";
import { map, tap } from "rxjs";

export const authGuard: CanActivateFn = () => {
    const authService = inject(AuthService);
    const router = inject(Router);
    const locale = inject(LocaleService);

    if (authService.isLoggedIn()) {
        return true;
    }

    return authService.refreshToken().pipe(
        tap((token) => {
            if (!token) {
                router.navigateByUrl(locale.path('/'));
            }
        }),
        map((token) => !!token)
    );
}
