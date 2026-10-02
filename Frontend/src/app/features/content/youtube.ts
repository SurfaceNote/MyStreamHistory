const videoIdPattern = /^[a-zA-Z0-9_-]{11}$/;

export function youtubeEmbedUrl(videoId: unknown): string | null {
  return typeof videoId === 'string' && videoId.length === 11 && videoIdPattern.test(videoId)
    ? `https://www.youtube.com/embed/${videoId}` : null;
}

export function youtubeVideoId(value: string): string | null {
  try {
    const url = new URL(value.trim());
    if (!['https:', 'http:'].includes(url.protocol) || url.username || url.password || url.port) return null;
    const host = url.hostname.toLowerCase();
    let id: string | null = null;
    if (host === 'youtu.be' || host === 'www.youtu.be') {
      id = /^\/([a-zA-Z0-9_-]{11})\/?$/.exec(url.pathname)?.[1] || null;
    } else if (['youtube.com', 'www.youtube.com', 'm.youtube.com', 'music.youtube.com'].includes(host)) {
      id = url.pathname === '/watch' ? url.searchParams.get('v')
        : /^\/(?:shorts|live|embed)\/([a-zA-Z0-9_-]{11})\/?$/.exec(url.pathname)?.[1] || null;
    } else if (host === 'youtube-nocookie.com' || host === 'www.youtube-nocookie.com') {
      id = /^\/embed\/([a-zA-Z0-9_-]{11})\/?$/.exec(url.pathname)?.[1] || null;
    }
    return id && youtubeEmbedUrl(id) ? id : null;
  } catch { return null; }
}
