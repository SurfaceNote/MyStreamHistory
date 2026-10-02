import { Editor } from '@tiptap/core';
import StarterKit from '@tiptap/starter-kit';
import { YoutubeVideo } from './youtube.extension';

describe('YouTube editor node clipboard handling', () => {
  it('preserves the video ID when copying its HTML back into the editor', () => {
    const editor = new Editor({ extensions: [StarterKit, YoutubeVideo], content: {
      type: 'doc', content: [{ type: 'youtube', attrs: { videoId: 'dQw4w9WgXcQ' } }]
    } });
    editor.commands.setContent(editor.getHTML());
    expect(editor.getJSON().content?.[0]).toEqual({ type: 'youtube', attrs: { videoId: 'dQw4w9WgXcQ' } });
    editor.destroy();
  });

  it('rejects a pasted video wrapper pointing to an arbitrary iframe URL', () => {
    const editor = new Editor({ extensions: [StarterKit, YoutubeVideo],
      content: '<div data-youtube-video><iframe src="https://evil.test/embed/dQw4w9WgXcQ"></iframe></div>' });
    expect(editor.getJSON().content?.some(node => node.type === 'youtube')).toBeFalse();
    expect(editor.getHTML()).not.toContain('iframe');
    editor.destroy();
  });
});
