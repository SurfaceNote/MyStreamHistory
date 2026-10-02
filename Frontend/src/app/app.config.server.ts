import { mergeApplicationConfig, ApplicationConfig } from '@angular/core';
import { provideServerRendering } from '@angular/platform-server';
import { appConfig } from './app.config';
import { CONTENT_API_BASE } from './features/content/content.service';
import { environment } from '../environments/environment';

const serverConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(),
    { provide: CONTENT_API_BASE, useValue: process.env['CONTENT_API_URL'] || environment.api_url },
  ]
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
