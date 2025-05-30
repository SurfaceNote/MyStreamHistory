import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { HomeComponent } from './home.component';
import { CallbackComponent } from '../../components/callback/callback.component';

const routes: Routes = [
  {path: '', component: HomeComponent},
  {path: 'callback', component: CallbackComponent}];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class HomeRoutingModule { }
