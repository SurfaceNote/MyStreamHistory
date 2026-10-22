import { Pipe, PipeTransform, inject } from '@angular/core';
import { LocaleService } from './locale.service';
import { TranslationKey } from './translations';
@Pipe({ name: 't', standalone: true, pure: false })
export class TranslatePipe implements PipeTransform {
  private readonly locale = inject(LocaleService);
  transform(key: TranslationKey, params?: Record<string, string | number>): string { return this.locale.t(key, params); }
}
@Pipe({ name: 'localPath', standalone: true, pure: false })
export class LocalPathPipe implements PipeTransform {
  private readonly locale = inject(LocaleService);
  transform(path: string | readonly unknown[]): string { return this.locale.path(path); }
}
