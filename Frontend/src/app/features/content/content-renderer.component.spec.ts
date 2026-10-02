import { TestBed } from '@angular/core/testing';
import { ContentRendererComponent } from './content-renderer.component';

describe('YouTube article rendering', () => {
  it('renders a responsive player for a saved video inside the document', () => {
    const fixture = TestBed.createComponent(ContentRendererComponent);
    fixture.componentRef.setInput('node', { type: 'doc', content: [
      { type: 'youtube', attrs: { videoId: 'dQw4w9WgXcQ' } }
    ] });
    fixture.detectChanges();
    const frame = fixture.nativeElement.querySelector('iframe');
    expect(frame.getAttribute('src')).toBe('https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ');
    expect(frame.hasAttribute('allowfullscreen')).toBeTrue();
    expect(frame.getAttribute('referrerpolicy')).toBe('strict-origin-when-cross-origin');
    expect(frame.parentElement.className).toBe('youtube-video');
    fixture.destroy();
  });

  it('does not trust arbitrary URLs and clears the player when the node changes', () => {
    const fixture = TestBed.createComponent(ContentRendererComponent);
    fixture.componentRef.setInput('node', { type: 'youtube', attrs: { videoId: 'dQw4w9WgXcQ' } });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('iframe')).not.toBeNull();
    fixture.componentRef.setInput('node', { type: 'youtube', attrs: { videoId: 'https://evil.test', src: 'https://evil.test' } });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('iframe')).toBeNull();
    fixture.destroy();
  });
});
