import { createServer } from 'node:http';
export const languages = ['en', 'ru', 'cs', 'de', 'es'];
export const titles = { en: 'English article', ru: 'Русская статья', cs: 'Český článek', de: 'Deutscher Artikel', es: 'Artículo español' };
export const prefix = language => language === 'en' ? '' : '/' + language;
export const streamId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
export function fixtureApi(raw) {
  const url = new URL(raw, 'http://localhost');
  if (url.pathname === '/content/sitemap.xml') return { contentType: 'application/xml', body: '<urlset>' + languages.map(code => '<url><loc>https://mystreamhistory.com' + prefix(code) + '/news</loc></url>').join('') + '</urlset>' };
  const language = url.searchParams.get('language') || 'en';
  if (url.pathname.endsWith('/missing') || url.pathname === '/user/999') return { status: 404, body: {} };
  if (url.pathname.endsWith('/broken') || url.pathname === '/user/998') return { status: 503, body: {} };
  const slug = url.pathname.split('/').at(-1);
  const summary = { id: 'article-' + language, language, type: 'news', slug: 'article', title: titles[language], summary: 'Summary', coverId: null, publishedAt: '2026-10-20T12:00:00Z' };
  if (url.pathname === '/content') return { body: { items: [summary], page: 1, total: 1, pageSize: 12 } };
  if (/^\/content\/(news|review)\/(article|solo|partial)$/.test(url.pathname)) return { body: {
    ...summary, slug, seoTitle: titles[language], seoDescription: titles[language],
    body: { type: 'doc', content: [{ type: 'paragraph', content: [{ type: 'text', text: titles[language] + ' body' }] }] },
    translations: (slug === 'solo' ? [language] : slug === 'partial' ? ['en', 'ru'] : languages).map(code => ({ id: 'article-' + code, language: code, path: prefix(code) + '/news/' + slug }))
  } };
  let data;
  if (['/user/all', '/user/get-new-users'].includes(url.pathname)) data = [{ twitchId: 123, displayName: 'Example', avatar: '', isLive: false }];
  else if (url.pathname === '/user/123') data = { twitchId: 123, displayName: 'Example', avatar: '' };
  else if (url.pathname === '/user/123/recent-streams') data = [{ id: streamId, streamerLogin: 'example', startedAt: '2026-10-20T12:00:00Z', endedAt: '2026-10-20T14:00:00Z', categories: [], isLive: false }];
  else if (url.pathname.endsWith('/top-viewers')) data = [];
  else if (url.pathname.endsWith('/social-links')) data = { socialLinks: [] };
  else if (url.pathname.endsWith('/statistics')) data = { totalStreamsCount: 1, totalUniqueGamesCount: 0, totalStreamedHours: 2, categories: [], playthroughs: [], dashboard: { period: '30d', totalStreamsCount: 1, totalStreamedHours: 2, totalUniqueGamesCount: 0, streamedHoursByDay: [{ date: '2026-10-20', value: 2 }], topCategories: [], chatPointsByDay: [], viewerCountByDay: [] } };
  else if (url.pathname === '/streams/' + streamId) data = { id: streamId, twitchUserId: 123, streamerDisplayName: 'Example', startedAt: '2026-10-20T12:00:00Z', endedAt: '2026-10-20T14:00:00Z', viewers: [], categories: [], audience: [{ startedAt: '2026-10-20T12:00:00Z', averageViewers: 10, sampleCount: 5 }] };
  else return { status: 404, body: {} };
  return { body: { success: true, data } };
}
export async function startFixtureApi() {
  const requests = [];
  const server = createServer((req, res) => {
    requests.push({ url: req.url, authorization: req.headers.authorization, cookie: req.headers.cookie });
    const result = fixtureApi(req.url); res.writeHead(result.status || 200, { 'Content-Type': result.contentType || 'application/json' }); res.end(result.contentType ? result.body : JSON.stringify(result.body));
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  return { server, requests, url: 'http://127.0.0.1:' + server.address().port };
}
