import { inject, InjectionToken } from '@angular/core';
import { HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { finalize, tap } from 'rxjs';

export interface SsrTrace { record(url: string, status: number, durationMs: number): void; }
export const SSR_TRACE = new InjectionToken<SsrTrace>('SSR_TRACE');
export const ssrTraceInterceptor: HttpInterceptorFn = (request, next) => {
  const trace = inject(SSR_TRACE, { optional: true });
  if (!trace) return next(request);
  const started = performance.now();
  let status = 0;
  return next(request).pipe(tap({
    next: event => { if (event instanceof HttpResponse) status = event.status; },
    error: error => { status = error.status || 0; }
  }), finalize(() => trace.record(request.url, status, performance.now() - started)));
};
