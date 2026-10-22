import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LocaleService } from './locale.service';
import { LanguageSuggestionService } from './language-suggestion.service';

@Component({
  selector: 'app-language-suggestion',
  standalone: true,
  imports: [RouterLink],
  template: `@if (suggestion.offer(); as offer) {
    <aside class="language-suggestion" [attr.lang]="offer.language"
           [attr.aria-label]="locale.t('languageSuggestion.label', {}, offer.language)">
      <p>{{ locale.t(offer.article ? 'languageSuggestion.article' : 'languageSuggestion.page', {}, offer.language) }}</p>
      <a class="suggestion-open" [routerLink]="locale.switchUrl(offer.language)" (click)="locale.rememberLanguage(offer.language)">
        {{ locale.t('languageSuggestion.open', {}, offer.language) }} <span aria-hidden="true">→</span>
      </a>
      <button class="suggestion-dismiss" type="button" (click)="suggestion.dismiss()">
        {{ locale.t('languageSuggestion.dismiss', {}, offer.language) }}
      </button>
    </aside>
  }`,
  styles: [`
    :host { display: block; }
    .language-suggestion {
      display: flex; align-items: center; flex-wrap: wrap; gap: 8px 20px;
      max-width: var(--page-width); margin: 16px auto 0; padding: 12px var(--page-gutter);
      background: #152237; border-block: 1px solid #91baff30; color: var(--text, #f0f4ff);
    }
    p { flex: 1 1 260px; margin: 0; font-size: 14px; line-height: 1.6; }
    a, button { min-height: 44px; display: inline-flex; align-items: center; font-size: 13px; }
    a { color: #c4b5fd; font-weight: 650; gap: 8px; text-decoration: underline; text-underline-offset: 3px; }
    button { padding: 6px 0; border: 0; background: transparent; color: #b1bdd4; cursor: pointer; }
    a:focus-visible, button:focus-visible { outline: 2px solid #c4b5fd; outline-offset: 4px; border-radius: 3px; }
    button:hover { color: white; }
    @media (max-width: 600px) { .language-suggestion { gap: 4px 16px; } p { flex-basis: 100%; } }
  `]
})
export class LanguageSuggestionComponent {
  readonly locale = inject(LocaleService);
  readonly suggestion = inject(LanguageSuggestionService);
}
