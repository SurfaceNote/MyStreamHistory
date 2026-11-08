import { inject, InjectionToken } from '@angular/core';
import { AuthService } from './auth.service';

export interface AdminIdentity {
  isAdmin(): boolean;
  getTwitchIdFromToken(): string | null;
}

// Public Twitch identities never acquire administrative privileges.
// The independent admin application provides its cookie-session identity instead.
export const ADMIN_IDENTITY = new InjectionToken<AdminIdentity>('ADMIN_IDENTITY', {
  providedIn: 'root', factory: () => {
    const auth = inject(AuthService);
    return { isAdmin: () => false, getTwitchIdFromToken: () => auth.getTwitchIdFromToken() };
  }
});
