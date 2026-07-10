import { Routes } from '@angular/router';
import { CallbackComponent } from './components/callback/callback.component';
import { StreamerProfileComponent } from './pages/streamer-profile/streamer-profile.component';
import { StreamDetailComponent } from './pages/stream-detail/stream-detail.component';
import { SettingsComponent } from './pages/settings/settings.component';
import { AdminComponent } from './pages/admin/admin.component';
import { authGuard } from './auth/auth.guard';
import { ViewerStatsComponent } from './pages/viewer-stats/viewer-stats.component';

export const routes: Routes = [
    { path: 'callback', component: CallbackComponent, data: { seo: { title: 'Signing in — MyStreamHistory', description: 'Completing Twitch sign-in.', noIndex: true } } },
    { path: 'profile/:twitchId/viewer/:viewerTwitchId', component: ViewerStatsComponent, data: { seo: { title: 'Viewer Statistics — MyStreamHistory', description: 'Detailed Twitch viewer activity and watch statistics.', noIndex: true } } },
    { path: 'profile/:twitchId', component: StreamerProfileComponent, data: { seo: { title: 'Streamer Profile — MyStreamHistory', description: 'Twitch streamer history, games, audience and channel performance.', type: 'profile' } } },
    { path: 'stream/:streamId', component: StreamDetailComponent, data: { seo: { title: 'Stream Details — MyStreamHistory', description: 'Stream timeline, categories and audience statistics.', type: 'article' } } },
    { path: 'settings', component: SettingsComponent, canActivate: [authGuard], data: { seo: { title: 'Settings — MyStreamHistory', description: 'Manage your MyStreamHistory profile and preferences.', noIndex: true } } },
    { path: 'admin', component: AdminComponent, canActivate: [authGuard], data: { seo: { title: 'Administration — MyStreamHistory', description: 'MyStreamHistory administration tools.', noIndex: true } } },
    {
        path: '',
        loadChildren: () => import('./pages/home/home.module').then(m => m.HomeModule)
    }
];
