import { Routes } from '@angular/router';
import { CallbackComponent } from './components/callback/callback.component';
import { StreamerProfileComponent } from './pages/streamer-profile/streamer-profile.component';
import { StreamDetailComponent } from './pages/stream-detail/stream-detail.component';

export const routes: Routes = [
    { path: 'callback', component: CallbackComponent },
    { path: 'profile/:twitchId', component: StreamerProfileComponent },
    { path: 'stream/:streamId', component: StreamDetailComponent },
    {
        path: '',
        loadChildren: () => import('./pages/home/home.module').then(m => m.HomeModule)
    }
];
