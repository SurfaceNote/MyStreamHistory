import { CanDeactivateFn, Routes } from '@angular/router';
import { CallbackComponent } from './components/callback/callback.component';
import { StreamerProfileComponent } from './pages/streamer-profile/streamer-profile.component';
import { StreamDetailComponent } from './pages/stream-detail/stream-detail.component';
import { SettingsComponent } from './pages/settings/settings.component';
import { AdminComponent } from './pages/admin/admin.component';
import { authGuard } from './auth/auth.guard';
import { adminGuard } from './auth/admin.guard';
import type { ArticleEditorComponent } from './features/content/article-editor.component';
import { ViewerStatsComponent } from './pages/viewer-stats/viewer-stats.component';
import { TopViewersOverlayComponent } from './pages/top-viewers-overlay/top-viewers-overlay.component';

const saveBeforeLeaving: CanDeactivateFn<ArticleEditorComponent> = component => component.flush();

export const routes: Routes = [
    { path: 'callback', component: CallbackComponent, data: { seo: { title: 'Signing in — MyStreamHistory', description: 'Completing Twitch sign-in.', noIndex: true } } },
    { path: 'overlay/top-viewers/:twitchId', component: TopViewersOverlayComponent, data: { seo: { title: 'Top Viewers Overlay — MyStreamHistory', description: 'OBS top viewers overlay.', noIndex: true } } },
    { path: 'profile/:twitchId/viewer/:viewerTwitchId', component: ViewerStatsComponent, data: { seo: { title: 'Viewer Statistics — MyStreamHistory', description: 'Detailed Twitch viewer activity and watch statistics.', noIndex: true } } },
    { path: 'profile/:twitchId', component: StreamerProfileComponent, data: { seo: { title: 'Streamer Profile — MyStreamHistory', description: 'Twitch streamer history, games, audience and channel performance.', type: 'profile' } } },
    { path: 'stream/:streamId', component: StreamDetailComponent, data: { seo: { title: 'Stream Details — MyStreamHistory', description: 'Stream timeline, categories and audience statistics.', type: 'article' } } },
    { path: 'settings', component: SettingsComponent, canActivate: [authGuard], data: { seo: { title: 'Settings — MyStreamHistory', description: 'Manage your MyStreamHistory profile and preferences.', noIndex: true } } },
    { path: 'admin', component: AdminComponent, canActivate: [adminGuard], data: { seo: { title: 'Diagnostics — MyStreamHistory', description: 'Administration tools.', noIndex: true } } },
    { path: 'admin/publications', loadComponent: () => import('./features/content/publications-admin.component').then(m => m.PublicationsAdminComponent), canActivate: [adminGuard], data: { seo: { title: 'Publications — MyStreamHistory', description: 'Editorial workspace.', noIndex: true } } },
    { path: 'admin/publications/:id/preview', loadComponent: () => import('./features/content/article-preview.component').then(m => m.ArticlePreviewComponent), canActivate: [adminGuard], data: { seo: { title: 'Draft preview — MyStreamHistory', description: 'Private article preview.', noIndex: true } } },
    { path: 'admin/publications/:id', loadComponent: () => import('./features/content/article-editor.component').then(m => m.ArticleEditorComponent), canActivate: [adminGuard], canDeactivate: [saveBeforeLeaving], data: { seo: { title: 'Article editor — MyStreamHistory', description: 'Editorial workspace.', noIndex: true } } },
    { path: 'news', loadComponent: () => import('./features/content/public-content.component').then(m => m.PublicContentComponent), data: { type: 'news', seo: { title: 'News — MyStreamHistory', description: 'Streaming news and updates.' } } },
    { path: 'reviews', loadComponent: () => import('./features/content/public-content.component').then(m => m.PublicContentComponent), data: { type: 'review', seo: { title: 'Reviews — MyStreamHistory', description: 'Streaming reviews and impressions.' } } },
    { path: 'news/:slug', loadComponent: () => import('./features/content/article-page.component').then(m => m.ArticlePageComponent), data: { type: 'news' } },
    { path: 'reviews/:slug', loadComponent: () => import('./features/content/article-page.component').then(m => m.ArticlePageComponent), data: { type: 'review' } },
    {
        path: '',
        loadChildren: () => import('./pages/home/home.module').then(m => m.HomeModule)
    }
];
