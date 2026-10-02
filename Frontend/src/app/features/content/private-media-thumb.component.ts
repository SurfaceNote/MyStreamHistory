import { CommonModule, isPlatformBrowser } from '@angular/common';
import { AfterViewInit, Component, ElementRef, Input, OnDestroy, PLATFORM_ID, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { MediaAsset } from './content.models';
import { ContentService } from './content.service';

@Component({
  selector: 'app-private-media-thumb', standalone: true, imports: [CommonModule],
  template: `<img *ngIf="url; else placeholder" [src]="url" [alt]="asset.fileName" /><ng-template #placeholder><span>{{ asset.contentType === 'image/gif' ? 'GIF' : '▣' }}</span></ng-template>`,
  styles: [`:host { display:grid; place-items:center; width:58px; height:58px; flex:none; overflow:hidden; background:#30253f; color:#bb8cfa; border-radius:7px; font-size:.75rem; font-weight:800; } img { width:100%; height:100%; object-fit:cover; }`]
})
export class PrivateMediaThumbComponent implements AfterViewInit, OnDestroy {
  @Input({ required: true }) asset!: MediaAsset;
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly content = inject(ContentService);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private observer: IntersectionObserver | null = null;
  private destroyed = false;
  url = '';

  ngAfterViewInit(): void {
    if (!this.browser || this.asset.contentType === 'image/gif') return;
    if (typeof IntersectionObserver === 'undefined') { void this.load(); return; }
    this.observer = new IntersectionObserver(entries => {
      if (entries.some(entry => entry.isIntersecting)) { this.observer?.disconnect(); void this.load(); }
    }, { rootMargin: '150px' });
    this.observer.observe(this.host.nativeElement);
  }
  ngOnDestroy(): void {
    this.destroyed = true;
    this.observer?.disconnect();
    if (this.url) URL.revokeObjectURL(this.url);
  }
  private async load(): Promise<void> {
    try {
      const blob = await firstValueFrom(this.content.mediaBlob(this.asset.id));
      const url = URL.createObjectURL(blob);
      if (this.destroyed) URL.revokeObjectURL(url);
      else this.url = url;
    } catch { /* Keep the file type placeholder when a thumbnail is unavailable. */ }
  }
}
