import { Pipe, PipeTransform, inject } from '@angular/core';
import { LocaleService } from '../../i18n/locale.service';
import { gameIdFromSegment, gamePath } from './game-path';

/**
 * Localized link to a game page: `[routerLink]="game.twitchId | gamePath: game.name"`.
 * Null for an id that is not a Twitch category id, which leaves the link without a target.
 */
@Pipe({ name: 'gamePath' })
export class GamePathPipe implements PipeTransform {
  private readonly locale = inject(LocaleService);

  transform(twitchId: string | null | undefined, name: string): string | null {
    return twitchId && gameIdFromSegment(twitchId) === twitchId ? this.locale.path(gamePath(twitchId, name)) : null;
  }
}
