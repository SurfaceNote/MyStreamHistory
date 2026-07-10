import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { HomeComponent } from './home.component';
import { CallbackComponent } from '../../components/callback/callback.component';

const routes: Routes = [
  {
    path: '',
    component: HomeComponent,
    data: {
      seo: {
        title: 'MyStreamHistory — Twitch Stream Analytics & History',
        description: 'Track Twitch stream history, games, audience activity and channel performance with clear analytics for streamers and their communities.'
      }
    }
  },
  {path: 'callback', component: CallbackComponent}];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class HomeRoutingModule { }
