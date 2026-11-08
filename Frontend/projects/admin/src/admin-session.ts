import { HttpClient, HttpInterceptorFn } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../src/environments/environment';

@Injectable({ providedIn: 'root' })
export class AdminSession {
  private readonly http = inject(HttpClient);
  readonly username = signal<string | null>(null);
  token = '';
  async csrf(): Promise<void> {
    this.token = (await firstValueFrom(this.http.get<{ token: string }>('/api/auth/csrf'))).token;
  }
  async check(): Promise<boolean> {
    try {
      const session = await firstValueFrom(this.http.get<{ username: string }>('/api/auth/session'));
      this.username.set(session.username);
      await this.csrf();
      return true;
    } catch { this.username.set(null); return false; }
  }
  async login(username: string, password: string): Promise<void> {
    await this.csrf();
    const session = await firstValueFrom(this.http.post<{ username: string }>('/api/auth/login', { username, password }));
    this.username.set(session.username);
    // Antiforgery request tokens are bound to the authenticated identity.
    await this.csrf();
  }
  async logout(): Promise<void> {
    await firstValueFrom(this.http.post('/api/auth/logout', {}));
    this.username.set(null); this.token = '';
  }
}

export const adminHttp: HttpInterceptorFn = (request, next) => {
  const session = inject(AdminSession);
  let url = request.url;
  // Reuse existing feature services; all admin traffic stays on the BFF's same origin.
  if (url.startsWith(environment.api_url + '/')) url = '/api' + url.slice(environment.api_url.length);
  if (!url.startsWith('/api/')) return next(request);
  const headers = request.headers.delete('Authorization');
  return next(request.clone({ url, withCredentials: true, headers:
    /^(GET|HEAD|OPTIONS)$/.test(request.method) ? headers : headers.set('X-CSRF-TOKEN', session.token) }));
};

export const sessionGuard: CanActivateFn = async (_route, state) => {
  const session = inject(AdminSession);
  const router = inject(Router);
  return await session.check() || router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};
