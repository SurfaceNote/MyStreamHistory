import { HttpClient } from '@angular/common/http';
import { inject, Injectable, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { API_ENDPOINTS } from '../constants/api-endpoints';

const VISIT_KEY = 'msh.activity.visit';

/**
 * Tells the server, at most once per UTC day and browser, that the signed-in
 * channel owner opened the site. Used only for the weekly engaged channels metric.
 */
@Injectable({ providedIn: 'root' })
export class ActivityService {
  private http = inject(HttpClient);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));

  recordVisit(): void {
    if (!this.browser) return;
    const today = new Date().toISOString().slice(0, 10);
    try {
      if (localStorage.getItem(VISIT_KEY) === today) return;
      localStorage.setItem(VISIT_KEY, today);
    } catch { /* Storage is optional; the server deduplicates as well. */ }
    this.http.post(API_ENDPOINTS.ACTIVITY_VISIT, {}).subscribe({ error: () => undefined });
  }
}
