import { DOCUMENT } from '@angular/common';
import { inject, Injectable } from '@angular/core';

type EventParameters = Record<string, string | number | boolean>;
type AnalyticsWindow = Window & {
  gtag?: (command: 'event', name: string, parameters: EventParameters) => void;
};

@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  private document = inject(DOCUMENT);

  trackEvent(name: string, parameters: EventParameters): void {
    const browser = this.document.defaultView as AnalyticsWindow | null;
    // The tag is absent on the server and on hosts excluded in index.html.
    browser?.gtag?.('event', name, parameters);
  }
}
