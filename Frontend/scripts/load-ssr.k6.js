import http from 'k6/http';
import { check, sleep } from 'k6';

// Explicit staging URL and representative existing paths are required.
if (!__ENV.BASE_URL || !__ENV.PATHS) throw new Error('Set BASE_URL and PATHS (comma-separated staging page paths).');
const paths = __ENV.PATHS.split(',');
export const options = {
  stages: [{ duration: '30s', target: 1 }, { duration: '1m', target: 5 }, { duration: '30s', target: 0 }],
  thresholds: { http_req_failed: ['rate<0.01'], checks: ['rate>0.99'] }
};
export default function () {
  const path = paths[Math.floor(Math.random() * paths.length)];
  const response = http.get(__ENV.BASE_URL.replace(/\/$/, '') + path, { tags: { name: 'public-page' } });
  check(response, { 'successful page': r => r.status === 200, 'SSR timing present': r => !!r.headers['Server-Timing'] });
  sleep(1);
}
