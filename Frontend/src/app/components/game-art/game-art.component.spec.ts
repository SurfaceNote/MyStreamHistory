import { TestBed } from '@angular/core/testing';
import { GameArtComponent } from './game-art.component';

describe('GameArtComponent', () => {
  it('renders a fallback for missing or failed covers and recovers when the source changes', () => {
    const fixture = TestBed.createComponent(GameArtComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('svg')).not.toBeNull();
    fixture.componentRef.setInput('src', '/cover-a.jpg');
    fixture.detectChanges();
    fixture.nativeElement.querySelector('img').dispatchEvent(new Event('error'));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('img')).toBeNull();
    expect(fixture.nativeElement.querySelector('svg')).not.toBeNull();
    fixture.componentRef.setInput('src', '/cover-b.jpg');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('img').getAttribute('src')).toBe('/cover-b.jpg');
  });

  it('resolves Twitch image dimensions and keeps the cover decorative', () => {
    const fixture = TestBed.createComponent(GameArtComponent);
    fixture.componentRef.setInput('src', '/game-{width}x{height}.jpg');
    fixture.detectChanges();
    const image = fixture.nativeElement.querySelector('img');
    expect(image.getAttribute('src')).toBe('/game-144x192.jpg');
    expect(image.getAttribute('alt')).toBe('');
    expect(fixture.nativeElement.getAttribute('aria-hidden')).toBe('true');
  });
});
