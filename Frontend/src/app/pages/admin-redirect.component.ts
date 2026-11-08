import { afterNextRender, Component, inject } from '@angular/core';
import { Router } from '@angular/router';

@Component({
  selector: 'app-admin-redirect',
  template: `<p style="padding:40px">Администрирование перенесено. <a [href]="target">Открыть панель управления</a></p>`
})
export class AdminRedirectComponent {
  private readonly router = inject(Router);
  readonly target = 'https://admin.mystreamhistory.com' +
    (this.router.getCurrentNavigation()?.finalUrl?.toString() || this.router.url);
  constructor() { afterNextRender(() => window.location.replace(this.target)); }
}
