import { HttpContextToken } from '@angular/common/http';
import { InjectionToken } from '@angular/core';
import { environment } from '../../../environments/environment';
export const API_BASE = new InjectionToken<string>('API_BASE', { providedIn: 'root', factory: () => environment.api_url });

// Public responses must be identical for anonymous SSR and the first browser render.
export const ANONYMOUS_REQUEST = new HttpContextToken<boolean>(() => false);
