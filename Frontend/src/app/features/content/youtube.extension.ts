import { Node } from '@tiptap/core';
import { youtubeEmbedUrl, youtubeVideoId } from './youtube';

export const YoutubeVideo = Node.create({
  addOptions() {
    return { labels: () => ({ invalid: 'Invalid YouTube video', player: 'YouTube video player' }) };
  },
  name: 'youtube',
  group: 'block',
  atom: true,
  draggable: true,
  addAttributes() {
    return {
      videoId: {
        default: null,
        parseHTML: element => youtubeVideoId(element.querySelector('iframe')?.getAttribute('src') || '')
      }
    };
  },
  parseHTML() {
    return [{ tag: 'div[data-youtube-video]', getAttrs: element =>
      youtubeVideoId(element.querySelector('iframe')?.getAttribute('src') || '') ? {} : false }];
  },
  renderHTML({ node }) {
    const src = youtubeEmbedUrl(node.attrs['videoId']);
    if (!src) return ['div', { 'data-youtube-video': '', class: 'youtube-video' }, this.options.labels().invalid];
    return ['div', { 'data-youtube-video': '', class: 'youtube-video' }, ['iframe', {
      src,
      title: this.options.labels().player,
      loading: 'lazy',
      allow: 'accelerometer; autoplay; encrypted-media; gyroscope; picture-in-picture; fullscreen',
      allowfullscreen: '',
      referrerpolicy: 'strict-origin-when-cross-origin'
    }]];
  }
});
