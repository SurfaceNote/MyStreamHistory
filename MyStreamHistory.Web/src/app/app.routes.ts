import { Routes } from '@angular/router';
import { CallbackComponent } from './components/callback/callback.component';

export const routes: Routes = [
    { path: 'callback', component: CallbackComponent },
    {
        path: '',
        loadChildren: () => import('./pages/home/home.module').then(m => m.HomeModule)
    }
];
