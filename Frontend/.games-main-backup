import { bootstrapApplication } from '@angular/platform-browser';
import * as Sentry from '@sentry/angular';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';
import { environment } from './environments/environment';

if (environment.sentry.dsn) {
  Sentry.init({
    dsn: environment.sentry.dsn,
    environment: environment.sentry.environment,
    release: environment.sentry.release || undefined,
    sendDefaultPii: false,
    integrations: [
      Sentry.browserTracingIntegration(),
      Sentry.replayIntegration({
        maskAllText: true,
        blockAllMedia: true
      })
    ],
    tracePropagationTargets: [environment.api_url],
    tracesSampleRate: environment.sentry.tracesSampleRate,
    replaysSessionSampleRate: 0,
    replaysOnErrorSampleRate: environment.sentry.replaysOnErrorSampleRate,
    beforeSend(event) {
      const request = event.request;

      if (request?.url) {
        try {
          const sanitizedUrl = new URL(request.url);
          sanitizedUrl.search = '';
          sanitizedUrl.hash = '';
          request.url = sanitizedUrl.toString();
        } catch {
          request.url = request.url.split('?')[0];
        }
      }

      if (request?.headers) {
        for (const headerName of Object.keys(request.headers)) {
          if (/authorization|cookie|token|secret/i.test(headerName)) {
            delete request.headers[headerName];
          }
        }
      }

      return event;
    }
  });
}

bootstrapApplication(AppComponent, appConfig)
  .catch((err) => {
    Sentry.captureException(err);
    console.error(err);
  });
