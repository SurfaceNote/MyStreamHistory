import { DOCUMENT } from '@angular/common';
import { Meta } from '@angular/platform-browser';
import { Injectable, InjectionToken, inject, signal } from '@angular/core';
export const SSR_RESPONSE = new InjectionToken<{ statusCode: number }>('SSR_RESPONSE');
@Injectable({ providedIn: 'root' })
export class ResponseStatusService {
  readonly failed = signal(false);
  reset(): void { this.failed.set(false); }
  private readonly document = inject(DOCUMENT);
  private readonly meta = inject(Meta);
  private response = inject(SSR_RESPONSE, { optional: true });
  fail(error: { status?: number }): void {
    this.failed.set(true);
    this.meta.updateTag({ name: 'robots', content: 'noindex, nofollow' });
    this.document.head.querySelectorAll('meta[property="og:locale:alternate"]').forEach(tag => tag.remove());
    this.document.head.querySelectorAll('link[rel="alternate"][hreflang]').forEach(link => link.remove());
    if (this.response) this.response.statusCode = error.status === 404 ? 404 : 502;
  }
}
