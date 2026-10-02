import {
  ApplicationConfig,
  ErrorHandler,
  inject,
  provideAppInitializer,
  provideZoneChangeDetection
} from '@angular/core';
import { provideRouter, withInMemoryScrolling } from '@angular/router';
import { createErrorHandler, TraceService } from '@sentry/angular';

import { routes } from './app.routes';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { authIntercerptor } from './auth/auth.interceptor';
import { provideClientHydration, withEventReplay } from '@angular/platform-browser';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }), 
    provideRouter(
      routes,
      withInMemoryScrolling({
        scrollPositionRestoration: 'top',
        anchorScrolling: 'enabled'
      })
    ),
    provideHttpClient(withFetch(), withInterceptors([authIntercerptor])),
    {
      provide: ErrorHandler,
      useValue: createErrorHandler({
        logErrors: true,
        showDialog: false
      })
    },
    provideAppInitializer(() => {
      inject(TraceService);
    }), provideClientHydration(withEventReplay())
  ]
};
