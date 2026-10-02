import { APP_BASE_HREF } from '@angular/common';
import { CommonEngine, isMainModule } from '@angular/ssr/node';
import express from 'express';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import bootstrap from './main.server';
import { environment } from './environments/environment';

const serverDistFolder = dirname(fileURLToPath(import.meta.url));
const browserDistFolder = resolve(serverDistFolder, '../browser');
const indexHtml = join(serverDistFolder, 'index.server.html');
const app = express();
const engine = new CommonEngine();
const apiBase = process.env['CONTENT_API_URL'] || environment.api_url;

app.get('/health/ready', (_req, res) => res.sendStatus(200));
app.use(express.static(browserDistFolder, { maxAge: '1y', index: false }));

// Only editorial pages need a server-rendered response. Existing app routes stay client-rendered.
app.get(/^\/(news|reviews)(?:\/([a-z0-9-]+))?\/?$/, async (req, res, next) => {
  const match = /^\/(news|reviews)(?:\/([a-z0-9-]+))?\/?$/.exec(req.path);
  if (!match) return next();
  const [, section, slug] = match;
  if (slug) {
    try {
      const result = await fetch(`${apiBase}/content/${section === 'news' ? 'news' : 'review'}/${slug}`);
      if (result.status === 404) return res.status(404).send('Article not found');
      if (!result.ok) return res.status(502).send('Article service unavailable');
    } catch { return res.status(502).send('Article service unavailable'); }
  }
  const { protocol, originalUrl, baseUrl, headers } = req;
  engine.render({
    bootstrap, documentFilePath: indexHtml,
    url: `${protocol}://${headers.host}${originalUrl}`,
    publicPath: browserDistFolder,
    providers: [{ provide: APP_BASE_HREF, useValue: baseUrl }]
  }).then(html => res.send(html)).catch(next);
});

app.get('*', (_req, res) => res.sendFile(join(browserDistFolder, 'index.csr.html')));

if (isMainModule(import.meta.url)) {
  app.listen(Number(process.env['PORT'] || 4000), '0.0.0.0');
}
