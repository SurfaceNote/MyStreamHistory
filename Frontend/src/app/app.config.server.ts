import { HTTP_TRANSFER_CACHE_ORIGIN_MAP } from '@angular/common/http';
import { mergeApplicationConfig, ApplicationConfig } from '@angular/core';
import { provideServerRendering } from '@angular/platform-server';
import { appConfig } from './app.config';
import { API_BASE } from './core/api/api-base';
import { environment } from '../environments/environment';

const apiBase = process.env['API_BASE_URL'] || process.env['CONTENT_API_URL'] || environment.api_url;
const serverConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(),
    { provide: API_BASE, useValue: apiBase },
    { provide: HTTP_TRANSFER_CACHE_ORIGIN_MAP, useValue: { [new URL(apiBase).origin]: new URL(environment.api_url).origin } },
  ]
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
