import { writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

function readSampleRate(name, fallback) {
  const rawValue = process.env[name];
  if (!rawValue) {
    return fallback;
  }

  const value = Number(rawValue);
  if (!Number.isFinite(value) || value < 0 || value > 1) {
    throw new Error(`${name} must be a number between 0 and 1.`);
  }

  return value;
}

const config = {
  dsn: process.env.SENTRY_DSN ?? '',
  environment: process.env.SENTRY_ENVIRONMENT || 'production',
  release: process.env.SENTRY_RELEASE ?? '',
  tracesSampleRate: readSampleRate('SENTRY_TRACES_SAMPLE_RATE', 0.05),
  replaysOnErrorSampleRate: readSampleRate('SENTRY_REPLAYS_ON_ERROR_SAMPLE_RATE', 1)
};

const targetPath = fileURLToPath(
  new URL('../src/environments/sentry.generated.ts', import.meta.url)
);

writeFileSync(
  targetPath,
  `// Generated during the production build. Do not put SENTRY_AUTH_TOKEN here.\n` +
    `export const sentryEnvironment = ${JSON.stringify(config, null, 2)} as const;\n`,
  'utf8'
);
