import { DOCUMENT } from '@angular/common';
import { inject, Injectable } from '@angular/core';

type EventParameters = Record<string, string | number | boolean>;
type GtagParameters = Record<string, string | number | boolean | (() => void)>;
type AnalyticsWindow = Window & {
  gtag?: (command: 'event', name: string, parameters: GtagParameters) => void;
  mshStartAnalytics?: () => void;
};

/** Longest wait before leaving the site: sign-in must not feel slow because of analytics. */
const LEAVE_TIMEOUT_MS = 600;

@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  private document = inject(DOCUMENT);

  private get browser(): AnalyticsWindow | null {
    return this.document.defaultView as AnalyticsWindow | null;
  }

  /** Starts the tag that index.html defers on the OAuth callback page. Repeated calls do nothing. */
  start(): void {
    this.browser?.mshStartAnalytics?.();
  }

  trackEvent(name: string, parameters: EventParameters = {}): void {
    // The tag is absent on the server and on hosts excluded in index.html.
    this.browser?.gtag?.('event', name, parameters);
  }

  /**
   * Sends an event right before the page is replaced (for example, by the Twitch sign-in page).
   * Resolves once the tag confirms the hit or after a short timeout, whichever comes first,
   * and immediately when the tag is absent or blocked.
   */
  trackEventBeforeLeaving(name: string, parameters: EventParameters = {}): Promise<void> {
    const gtag = this.browser?.gtag;
    if (!gtag) return Promise.resolve();
    return new Promise(resolve => {
      const timer = setTimeout(resolve, LEAVE_TIMEOUT_MS);
      const done = () => { clearTimeout(timer); resolve(); };
      gtag('event', name, { ...parameters, event_callback: done, event_timeout: LEAVE_TIMEOUT_MS });
    });
  }
}
