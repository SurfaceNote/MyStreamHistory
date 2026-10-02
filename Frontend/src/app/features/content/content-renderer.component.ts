import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { ContentNode } from './content.models';

@Component({
  selector: 'app-content-renderer',
  standalone: true,
  imports: [CommonModule],
  template: `
    <ng-container [ngSwitch]="node.type">
      <ng-container *ngSwitchCase="'doc'"><ng-container *ngFor="let child of node.content"><app-content-renderer [node]="child" /></ng-container></ng-container>
      <p *ngSwitchCase="'paragraph'"><ng-container *ngFor="let child of node.content"><app-content-renderer [node]="child" /></ng-container></p>
      <ng-container *ngSwitchCase="'heading'"><h3 *ngIf="node.attrs?.['level'] === 3; else secondHeading"><ng-container *ngFor="let child of node.content"><app-content-renderer [node]="child" /></ng-container></h3><ng-template #secondHeading><h2><ng-container *ngFor="let child of node.content"><app-content-renderer [node]="child" /></ng-container></h2></ng-template></ng-container>
      <ul *ngSwitchCase="'bulletList'"><ng-container *ngFor="let child of node.content"><app-content-renderer [node]="child" /></ng-container></ul>
      <ol *ngSwitchCase="'orderedList'" [attr.start]="node.attrs?.['start'] || null" [attr.type]="node.attrs?.['type'] || null"><ng-container *ngFor="let child of node.content"><app-content-renderer [node]="child" /></ng-container></ol>
      <li *ngSwitchCase="'listItem'"><ng-container *ngFor="let child of node.content"><app-content-renderer [node]="child" /></ng-container></li>
      <blockquote *ngSwitchCase="'blockquote'"><ng-container *ngFor="let child of node.content"><app-content-renderer [node]="child" /></ng-container></blockquote>
      <ng-container *ngSwitchCase="'text'">
        <a *ngIf="link; else plain" [href]="link" rel="noopener noreferrer" target="_blank"><strong *ngIf="hasMark('bold'); else normalLink"><em *ngIf="hasMark('italic'); else normalBold">{{ node.text }}</em><ng-template #normalBold>{{ node.text }}</ng-template></strong><ng-template #normalLink><em *ngIf="hasMark('italic'); else normalText">{{ node.text }}</em><ng-template #normalText>{{ node.text }}</ng-template></ng-template></a>
        <ng-template #plain><strong *ngIf="hasMark('bold'); else noBold"><em *ngIf="hasMark('italic'); else onlyBold">{{ node.text }}</em><ng-template #onlyBold>{{ node.text }}</ng-template></strong><ng-template #noBold><em *ngIf="hasMark('italic'); else noMark">{{ node.text }}</em><ng-template #noMark>{{ node.text }}</ng-template></ng-template></ng-template>
      </ng-container>
      <img *ngSwitchCase="'image'" [src]="node.attrs?.['src']" [alt]="node.attrs?.['alt'] || ''" loading="lazy" />
      <br *ngSwitchCase="'hardBreak'" />
    </ng-container>
  `,
  styles: [`:host { display: contents; } p { line-height: 1.8; margin: 1.25em 0; } h2 { margin: 1.8em 0 .6em; font-size: 1.6em; } h3 { margin: 1.5em 0 .5em; font-size: 1.3em; } img { display: block; max-width: 100%; border-radius: 14px; margin: 1.8em auto; } blockquote { border-left: 3px solid #a970ff; margin: 1.5em 0; padding-left: 1.1em; color: #c8c4d0; } a { color: #b68aff; } li { line-height: 1.7; }`]
})
export class ContentRendererComponent {
  @Input({ required: true }) node!: ContentNode;
  hasMark(type: string): boolean { return !!this.node.marks?.some(mark => mark.type === type); }
  get link(): string | null {
    const href = this.node.marks?.find(mark => mark.type === 'link')?.attrs?.['href'];
    return typeof href === 'string' && /^https?:\/\//i.test(href) ? href : null;
  }
}
