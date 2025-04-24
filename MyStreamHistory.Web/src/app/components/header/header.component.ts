import { Component } from '@angular/core';
import { LoginComponentComponent } from '../buttons/login-component/login-component.component';

@Component({
  selector: 'app-header',
  imports: [LoginComponentComponent],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss'
})
export class HeaderComponent {

}
