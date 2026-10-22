import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { createServer } from 'node:net';
import { fileURLToPath } from 'node:url';
import { languages, titles, prefix, streamId, startFixtureApi } from './localization-fixture.mjs';
export async function startSsr() {
  const api = await startFixtureApi();
  const reserve = createServer(); await new Promise(resolve => reserve.listen(0, '127.0.0.1', resolve));
  const port = reserve.address().port; await new Promise(resolve => reserve.close(resolve));
  const child = spawn(process.execPath, ['dist/my-stream-history.web/server/server.mjs'], {
    cwd: fileURLToPath(new URL('../', import.meta.url)), windowsHide: true,
    env: { ...process.env, API_BASE_URL: api.url, PORT: String(port) }, stdio: ['ignore', 'pipe', 'pipe']
  });
  let log = ''; child.stdout.on('data', value => log = (log + value).slice(-6000)); child.stderr.on('data', value => log = (log + value).slice(-6000));
  const origin = 'http://127.0.0.1:' + port;
  for (let i = 0; i < 100; i++) {
    try { if ((await fetch(origin + '/health/ready')).ok) return { ...api, child, origin, log: () => log }; } catch { /* Wait for this child only. */ }
    if (child.exitCode !== null) throw new Error('SSR exited: ' + log);
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  child.kill(); api.server.close(); throw new Error('SSR did not start: ' + log);
}
export async function smokeSsr(runtime) {
  const request = runtime.request || fetch;
  for (const [headers, expected] of [
    [{ 'Accept-Language': 'ru-RU,ru;q=0.9,en;q=0.8' }, '/ru/?from=entry'],
    [{ 'Accept-Language': 'fr-FR,cs-CZ;q=0.9,de;q=0.7' }, '/cs/?from=entry'],
    [{ 'Accept-Language': 'ru', Cookie: 'preferred_language=es' }, '/es/?from=entry'],
    [{ 'Accept-Language': 'ru', Cookie: 'preferred_language=en' }, null],
    [{ 'Accept-Language': 'fr,ja' }, null],
    [{ 'Accept-Language': 'de', Cookie: 'preferred_language=invalid' }, '/de/?from=entry']
  ]) {
    const response = await request(runtime.origin + '/?from=entry', { headers, redirect: 'manual' });
    assert.equal(response.status, expected ? 302 : 200);
    if (expected) assert.equal(response.headers.get('location'), expected);
    else assert.match(await response.text(), /<html[^>]*lang="en"/);
    assert.match(response.headers.get('cache-control'), /no-store/);
    assert.match(response.headers.get('vary'), /Accept-Language/i);
    assert.match(response.headers.get('vary'), /Cookie/i);
  }
  // Direct URLs always beat both a saved preference and browser settings.
  for (const [path, language] of [['/news/article', 'en'], ['/de/profile/123', 'de'], ['/ru/', 'ru']]) {
    const response = await request(runtime.origin + path, { headers: { 'Accept-Language': 'cs', Cookie: 'preferred_language=es' }, redirect: 'manual' });
    assert.equal(response.status, 200);
    assert.match(await response.text(), new RegExp('<html[^>]*lang="' + language + '"'));
  }
  const requests = languages.flatMap(language => ['', '/news', '/news/article', '/profile/123', '/stream/' + streamId].map(path => ({ language, path: prefix(language) + (path || '/') })));
  await Promise.all(requests.map(async ({ language, path }) => {
    const response = await request(runtime.origin + path, { headers: { Cookie: 'private-test-cookie=secret', Authorization: 'Bearer test-private' } });
    const html = await response.text(); assert.equal(response.status, 200, path + '\n' + runtime.log());
    assert.match(html, new RegExp('<html[^>]*lang="' + language + '"'), path);
    assert.ok(html.includes('rel="canonical" href="https://mystreamhistory.com' + path + '"'), path + ': canonical');
    assert.ok(html.includes('rel="alternate" hreflang="' + language + '"'), path + ': self hreflang');
    if (path.endsWith('/')) assert.match(html, /<section[^>]*class="intro"/, path + ': missing homepage content');
    if (path.endsWith('/article')) { assert.ok(html.includes(titles[language] + ' body'), path); }
    if (path.includes('/profile/') || path.includes('/stream/')) assert.ok(html.includes('Example'), path + ': missing public data');
    assert.ok(!html.includes('private-test-cookie'), path + ': leaked cookie');
    assert.ok(!html.includes('Bearer test-private'), path + ': leaked auth');
    assert.ok(!/<aside[^>]*class="language-suggestion"/.test(html), path + ': personalized suggestion in SSR');
  }));
  for (const [path, status] of [['/ru/news/missing',404],['/de/news/broken',502],['/cs/profile/999',404],['/es/profile/998',502],['/ru/unknown',404]]) {
    const response = await request(runtime.origin + path); const html = await response.text();
    assert.equal(response.status, status, path + '\n' + runtime.log()); assert.ok(!/<link[^>]*hreflang=/.test(html), path + ': error alternatives');
  }
  const partial = await (await request(runtime.origin + '/ru/news/partial')).text();
  assert.ok(!partial.includes('rel="alternate" hreflang="cs"'));
  const independent = await (await request(runtime.origin + '/ru/news/solo')).text();
  assert.ok(!independent.includes('rel="alternate" hreflang="en"')); assert.ok(!independent.includes('hreflang="x-default"'));
  for (const path of ['/settings','/admin/publications','/ru/profile/123/viewer/456']) {
    const response = await request(runtime.origin + path); assert.equal(response.headers.get('x-robots-tag'),'noindex');
  }
  const redirect = await request(runtime.origin + '/ru?x=1', { redirect: 'manual' }); assert.equal(redirect.status,301); assert.equal(redirect.headers.get('location'),'/ru/?x=1');
  assert.ok(runtime.requests.every(request => !request.authorization && !request.cookie), 'SSR forwarded private credentials');
  console.log('SSR localization smoke passed: 25 concurrent pages, status codes, alternatives, homepage negotiation, redirects and anonymous requests.');
}
if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const runtime = await startSsr();
  try { await smokeSsr(runtime); } finally { runtime.child.kill(); runtime.server.close(); }
}
