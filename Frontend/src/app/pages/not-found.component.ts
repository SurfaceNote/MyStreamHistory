import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LocaleService } from '../i18n/locale.service';
import { TranslatePipe } from '../i18n/translate.pipe';
import { SeoService } from '../service/seo.service';
import { ResponseStatusService } from '../core/api/response-status.service';
@Component({ standalone: true, imports: [TranslatePipe, RouterLink],
  template: `<main style="max-width:900px;margin:60px auto;padding:24px"><h1>{{ 'error.notFound' | t }}</h1><a [routerLink]="locale.path('/')">MyStreamHistory</a></main>` })
export class NotFoundComponent {
  readonly locale = inject(LocaleService);
  constructor() {
    inject(ResponseStatusService).fail({ status: 404 });
    inject(SeoService).update({ title: this.locale.t('error.notFound'), description: this.locale.t('error.notFound'), noIndex: true });
  }
}
