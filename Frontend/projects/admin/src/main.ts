import { inject, provideZoneChangeDetection } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { CanDeactivateFn, provideRouter, Routes } from '@angular/router';
import { API_BASE } from '../../../src/app/core/api/api-base';
import { ADMIN_IDENTITY } from '../../../src/app/auth/admin-identity';
import { UI_LANGUAGE } from '../../../src/app/i18n/locale.service';
import type { ArticleEditorComponent } from '../../../src/app/features/content/article-editor.component';
import { AdminAppComponent } from './app.component';
import { AdminSession, adminHttp, sessionGuard } from './admin-session';
const saveBeforeLeaving: CanDeactivateFn<ArticleEditorComponent> = component => component.flush();
const routes: Routes = [
  { path: 'login', loadComponent: () => import('./login.component').then(m => m.LoginComponent) },
  { path: '', canActivateChild: [sessionGuard], children: [
    { path: 'overview', loadComponent: () => import('./overview.component').then(m => m.OverviewComponent) },
    { path: 'streams', loadComponent: () => import('./streams-page.component').then(m => m.StreamsPageComponent) },
    { path: 'admin', loadComponent: () => import('../../../src/app/pages/admin/admin.component').then(m => m.AdminComponent) },
    { path: 'admin/publications', loadComponent: () => import('../../../src/app/features/content/publications-admin.component').then(m => m.PublicationsAdminComponent) },
    { path: 'admin/publications/:id/preview', loadComponent: () => import('../../../src/app/features/content/article-preview.component').then(m => m.ArticlePreviewComponent) },
    { path: 'admin/publications/:id', loadComponent: () => import('../../../src/app/features/content/article-editor.component').then(m => m.ArticleEditorComponent), canDeactivate: [saveBeforeLeaving] },
    { path: 'community', loadComponent: () => import('./community-page.component').then(m => m.CommunityPageComponent) },
    { path: 'monitoring', loadComponent: () => import('./monitoring.component').then(m => m.MonitoringComponent) },
    { path: '', pathMatch: 'full', redirectTo: 'overview' }
  ] },
  { path: '**', redirectTo: 'overview' }
];
bootstrapApplication(AdminAppComponent, { providers: [
  provideZoneChangeDetection({ eventCoalescing: true }), provideRouter(routes),
  provideHttpClient(withFetch(), withInterceptors([adminHttp])), { provide: API_BASE, useValue: '/api' },
  { provide: UI_LANGUAGE, useValue: 'ru' },
  { provide: ADMIN_IDENTITY, useFactory: () => {
    const session = inject(AdminSession);
    return { isAdmin: () => !!session.username(), getTwitchIdFromToken: () => null };
  } }
] }).catch(console.error);
