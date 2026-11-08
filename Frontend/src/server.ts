import { APP_BASE_HREF } from '@angular/common';
import { CommonEngine, isMainModule } from '@angular/ssr/node';
import express from 'express';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import bootstrap from './main.server';
import { SSR_RESPONSE } from './app/core/api/response-status.service';
import { environment } from './environments/environment';
import { acceptLanguage, languageCookie } from './app/i18n/language-preference';
import { localizedPath } from './app/i18n/languages';
import { SSR_TRACE } from './app/core/api/ssr-trace';
import { apiRoute, authorizedMetrics, pageRoute, SsrMetrics } from './ssr-observability';
import { randomUUID } from 'node:crypto';

const serverDistFolder = dirname(fileURLToPath(import.meta.url));
const browserDistFolder = resolve(serverDistFolder, '../browser');
const indexHtml = join(serverDistFolder, 'index.server.html');
const app = express();
const engine = new CommonEngine();
const metrics = new SsrMetrics();
app.get('/health/ready', (_req, res) => res.sendStatus(200));
app.get('/internal/metrics', (req, res) => {
  if (!authorizedMetrics(req.headers.authorization, process.env['METRICS_TOKEN'])) return res.sendStatus(404);
  return res.type('text/plain; version=0.0.4').send(metrics.text());
});
app.use(express.static(browserDistFolder, { maxAge: '1y', index: false }));
app.get('*', (req, res, next) => {
  // Only the bare entry point negotiates language. Every content URL remains stable.
  if (req.path === '/') {
    res.vary('Accept-Language');
    res.vary('Cookie');
    res.setHeader('Cache-Control', 'private, no-store');
    const language = languageCookie(req.headers.cookie) || acceptLanguage(req.headers['accept-language']);
    if (language !== 'en') return res.redirect(302, localizedPath(req.originalUrl, language));
  }
  if (/^\/(ru|cs|de|es)$/.test(req.path)) return res.redirect(301, req.path + '/' + (req.url.includes('?') ? req.url.slice(req.url.indexOf('?')) : ''));
  if (req.path.length > 1 && req.path.endsWith('/')) {
    if (!/^\/(ru|cs|de|es)\/$/.test(req.path)) return res.redirect(301, req.path.slice(0, -1) + (req.url.includes('?') ? req.url.slice(req.url.indexOf('?')) : ''));
  }
  if (/^\/(callback|settings|analytics\/community|admin(?:\/publications(?:\/[^/]+(?:\/preview)?)?)?|overlay\/top-viewers\/[^/]+)$/.test(req.path) ||
      /^\/(?:ru\/|cs\/|de\/|es\/)?profile\/[^/]+\/viewer\/[^/]+$/.test(req.path)) {
    res.setHeader('X-Robots-Tag', 'noindex');
    return res.sendFile(join(browserDistFolder, 'index.csr.html'));
  }
  const started = performance.now();
  const requestId = randomUUID();
  const requests: { route: string; status: number; durationMs: number; startMs: number }[] = [];
  let bytes = 0;
  metrics.active++;
  res.setHeader('X-Request-ID', requestId);
  return engine.render({
    bootstrap, documentFilePath: indexHtml,
    // Use the configured public origin, not an untrusted Host header, for SEO.
    url: new URL(req.originalUrl, environment.url).toString(),
    publicPath: browserDistFolder,
    providers: [{ provide: APP_BASE_HREF, useValue: '/' }, { provide: SSR_RESPONSE, useValue: res },
      { provide: SSR_TRACE, useValue: { record(url: string, status: number, durationMs: number) {
        metrics.api(url, status, durationMs);
        requests.push({ route: apiRoute(url), status, durationMs: Math.round(durationMs), startMs: Math.round(performance.now() - durationMs - started) });
      } } }]
  }).then(html => {
    bytes = Buffer.byteLength(html);
    res.setHeader('Server-Timing', `ssr;dur=${(performance.now() - started).toFixed(1)}`);
    return res.send(html);
  }).catch(error => { if (res.statusCode < 400) res.statusCode = 500; next(error); }).finally(() => {
    metrics.active--;
    const durationMs = performance.now() - started;
    metrics.render(req.path, res.statusCode, durationMs, bytes);
    console.log(JSON.stringify({ event: 'ssr.request', requestId, ...pageRoute(req.path), status: res.statusCode,
      durationMs: Math.round(durationMs), htmlBytes: bytes, apiRequests: requests.length, dependencies: requests }));
  });
});

if (isMainModule(import.meta.url)) {
  app.listen(Number(process.env['PORT'] || 4000), '0.0.0.0');
}
