import { monitorEventLoopDelay } from 'node:perf_hooks';
import { timingSafeEqual } from 'node:crypto';

export function pageRoute(path: string): { route: string; language: string } {
  const match = /^\/(ru|cs|de|es)(?=\/|$)/.exec(path);
  const clean = (match ? path.slice(match[0].length) : path) || '/';
  const language = match?.[1] || 'en';
  if (clean === '/') return { route: '/', language };
  for (const type of ['profile', 'stream', 'news', 'reviews']) {
    if (clean === '/' + type && (type === 'news' || type === 'reviews')) return { route: clean, language };
    if (new RegExp('^/' + type + '/[^/]+$').test(clean)) return { route: '/' + type + '/:id', language };
  }
  return { route: '/other', language };
}

export function apiRoute(url: string): string {
  const path = new URL(url, 'https://api.invalid').pathname;
  if (/^\/user\/(all|get-new-users)$/.test(path)) return path;
  if (/^\/user\/[^/]+$/.test(path)) return '/user/:id';
  const user = /^\/user\/[^/]+\/(recent-streams|top-viewers|social-links|statistics)$/.exec(path);
  if (user) return '/user/:id/' + user[1];
  if (/^\/streams\/[^/]+$/.test(path)) return '/streams/:id';
  if (path === '/content') return path;
  if (/^\/content\/(news|review)\/[^/]+$/.test(path)) return '/content/:type/:slug';
  return '/other';
}

export function authorizedMetrics(header: string | undefined, secret: string | undefined): boolean {
  if (!secret || !header) return false;
  const expected = Buffer.from('Bearer ' + secret), supplied = Buffer.from(header);
  return supplied.length === expected.length && timingSafeEqual(supplied, expected);
}

const boundaries = [.025, .05, .1, .25, .5, 1, 2.5, 5, 10, 30, 60];
class Histogram {
  private readonly rows = new Map<string, { labels: Record<string, string>; count: number; sum: number; buckets: number[] }>();
  observe(labels: Record<string, string>, value: number): void {
    const key = JSON.stringify(labels);
    let row = this.rows.get(key);
    if (!row) { row = { labels, count: 0, sum: 0, buckets: boundaries.map(() => 0) }; this.rows.set(key, row); }
    row.count++; row.sum += value;
    boundaries.forEach((limit, index) => { if (value <= limit) row!.buckets[index]++; });
  }
  render(name: string): string[] {
    const lines = ['# TYPE ' + name + ' histogram'];
    for (const row of this.rows.values()) {
      const labels = Object.entries(row.labels).map(([key, value]) => key + '=' + JSON.stringify(value)).join(',');
      boundaries.forEach((limit, index) => lines.push(`${name}_bucket{${labels},le="${limit}"} ${row.buckets[index]}`));
      lines.push(`${name}_bucket{${labels},le="+Inf"} ${row.count}`, `${name}_sum{${labels}} ${row.sum}`, `${name}_count{${labels}} ${row.count}`);
    }
    return lines;
  }
}

export class SsrMetrics {
  private readonly renders = new Histogram();
  private readonly apis = new Histogram();
  private readonly loop = monitorEventLoopDelay({ resolution: 20 });
  private htmlBytes = 0;
  active = 0;
  constructor() { this.loop.enable(); }
  api(url: string, status: number, durationMs: number): void {
    this.apis.observe({ route: apiRoute(url), status_class: status ? Math.floor(status / 100) + 'xx' : 'network' }, durationMs / 1000);
  }
  render(path: string, status: number, durationMs: number, bytes: number): void {
    this.renders.observe({ ...pageRoute(path), status_class: Math.floor(status / 100) + 'xx' }, durationMs / 1000);
    this.htmlBytes += bytes;
  }
  text(): string {
    const cpu = process.cpuUsage();
    const loopDelay = this.loop.percentile(95) / 1e9;
    this.loop.reset();
    return [...this.renders.render('msh_ssr_render_duration_seconds'), ...this.apis.render('msh_ssr_api_duration_seconds'),
      '# TYPE msh_ssr_html_bytes_total counter', `msh_ssr_html_bytes_total ${this.htmlBytes}`,
      '# TYPE msh_ssr_active_requests gauge', `msh_ssr_active_requests ${this.active}`,
      '# TYPE msh_ssr_process_cpu_seconds_total counter', `msh_ssr_process_cpu_seconds_total ${(cpu.user + cpu.system) / 1e6}`,
      '# TYPE msh_ssr_process_resident_memory_bytes gauge', `msh_ssr_process_resident_memory_bytes ${process.memoryUsage().rss}`,
      '# TYPE msh_ssr_event_loop_delay_seconds gauge', `msh_ssr_event_loop_delay_seconds ${loopDelay}`,
      ''].join('\n');
  }
}
