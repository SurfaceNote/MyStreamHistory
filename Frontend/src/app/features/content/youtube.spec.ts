import { youtubeEmbedUrl, youtubeVideoId } from './youtube';

describe('YouTube URL validation', () => {
  for (const url of [
    'https://www.youtube.com/watch?v=dQw4w9WgXcQ&feature=shared',
    ' https://youtu.be/dQw4w9WgXcQ?si=share-token ',
    'https://m.youtube.com/watch?v=dQw4w9WgXcQ',
    'https://youtube.com/shorts/dQw4w9WgXcQ',
    'https://www.youtube.com/live/dQw4w9WgXcQ',
    'https://www.youtube.com/embed/dQw4w9WgXcQ',
    'https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ'
  ]) {
    it(`extracts the video from ${url}`, () => expect(youtubeVideoId(url)).toBe('dQw4w9WgXcQ'));
  }

  for (const url of [
    '', 'not a URL', 'javascript:alert(1)', 'https://example.com/watch?v=dQw4w9WgXcQ',
    'https://www.youtube.com.evil.test/watch?v=dQw4w9WgXcQ',
    'https://youtube.com@evil.test/watch?v=dQw4w9WgXcQ',
    'https://evil.test@youtube.com/watch?v=dQw4w9WgXcQ',
    'https://youtube.com:8080/watch?v=dQw4w9WgXcQ',
    'https://youtu.be/dQw4w9WgXcQ/extra', 'https://youtube.com/playlist?list=dQw4w9WgXcQ',
    'https://youtube.com/watch?v=short', 'https://youtube.com/watch?v=dQw4w9WgXcQ%0A'
  ]) {
    it(`rejects ${url}`, () => expect(youtubeVideoId(url)).toBeNull());
  }

  it('constructs embed URLs only from validated IDs', () => {
    expect(youtubeEmbedUrl('aB_012345-9')).toBe('https://www.youtube-nocookie.com/embed/aB_012345-9');
    for (const id of [null, {}, 'https://evil.test', 'dQw4w9WgXcQ?autoplay=1', 'dQw4w9WgXcQ\n'])
      expect(youtubeEmbedUrl(id)).toBeNull();
  });
});
