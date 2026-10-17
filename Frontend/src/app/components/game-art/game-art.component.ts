import { Component, Input, OnChanges } from '@angular/core';

/** Decorative cover: the adjacent game name is always the accessible label. */
@Component({
  selector: 'app-game-art',
  host: { 'aria-hidden': 'true' },
  template: `
    @if (src && !failed) {
      <img [src]="imageUrl" alt="" loading="lazy" (error)="failed = true">
    } @else {
      <svg viewBox="0 0 32 32" fill="none" stroke="currentColor" stroke-width="1.5">
        <path d="M11 10h10c3 0 5 4 6 11 .5 3-2 4-4 2l-4-4h-6l-4 4c-2 2-4.5 1-4-2 1-7 3-11 6-11Z"/>
        <path d="M11 13v6m-3-3h6m7-1h.01m3 3h.01" stroke-linecap="round"/>
      </svg>
    }
  `,
  styles: `
    :host { display: grid; place-items: center; width: var(--game-art-width, 60px);
      aspect-ratio: 3 / 4; flex-shrink: 0; overflow: hidden; border-radius: 4px;
      background: #29292f; color: #8d8d9a; }
    img { display: block; width: 100%; height: 100%; object-fit: cover; }
    svg { width: 60%; max-width: 32px; }
  `
})
export class GameArtComponent implements OnChanges {
  @Input() src: string | null | undefined = '';
  failed = false;

  get imageUrl(): string {
    return (this.src || '').replace(/\{width\}/g, '144').replace(/\{height\}/g, '192');
  }

  ngOnChanges(): void { this.failed = false; }
}
