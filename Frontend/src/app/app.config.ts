import { ApplicationConfig, provideAppInitializer, provideZoneChangeDetection } from '@angular/core';
import { provideRouter, withInMemoryScrolling } from '@angular/router';

import { routes } from './app.routes';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { authIntercerptor } from './auth/auth.interceptor';
import { provideClientHydration } from '@angular/platform-browser';
import { appInitializer } from './core/app-initializer';

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
    provideHttpClient(withInterceptors([authIntercerptor])),
    provideClientHydration(),
    {
      provide: provideAppInitializer,
      useFactory: appInitializer,
      multi: true
    }
  ]
};
