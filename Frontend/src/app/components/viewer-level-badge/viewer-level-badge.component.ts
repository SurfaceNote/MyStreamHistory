import { Component, computed, inject, input } from '@angular/core';
import { LocaleService } from '../../i18n/locale.service';
import { TranslationKey } from '../../i18n/translations';

export const MAX_VIEWER_LEVEL = 20;

/** Badge for a viewer's channel level; artwork lives in public/images/levels (scripts/generate-level-icons.mjs). */
@Component({
  selector: 'app-viewer-level-badge',
  template: `<img [src]="src()" [alt]="label()" [title]="label()" [width]="size()" [height]="size()" loading="lazy" decoding="async">`,
  styles: `
    :host { display: inline-flex; flex-shrink: 0; line-height: 0; }
    img { display: block; width: 100%; height: 100%; }
  `,
  host: { '[style.width.px]': 'size()', '[style.height.px]': 'size()' }
})
export class ViewerLevelBadgeComponent {
  private readonly locale = inject(LocaleService);

  readonly level = input.required<number>();
  readonly size = input(24);

  readonly safeLevel = computed(() => Math.min(MAX_VIEWER_LEVEL, Math.max(1, Math.trunc(this.level() || 1))));
  readonly src = computed(() => `/images/levels/${this.safeLevel()}.svg`);
  readonly label = computed(() => {
    const level = this.safeLevel();
    return this.locale.t('level.badge', { level, title: this.locale.t(`level.${level}` as TranslationKey) });
  });
}
