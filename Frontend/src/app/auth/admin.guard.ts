import { LocaleService } from '../i18n/locale.service';
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from './auth.service';

export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const locale = inject(LocaleService);
  if (auth.isLoggedIn()) return auth.isAdmin() ? true : router.parseUrl(locale.path('/'));
  return auth.refreshToken().pipe(
    map(() => auth.isAdmin() ? true : router.parseUrl(locale.path('/'))),
    catchError(() => of(router.parseUrl(locale.path('/'))))
  );
};
