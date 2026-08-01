// This file contains safe local defaults. The production Docker build rewrites it
// from GitHub Actions build arguments before Angular compilation.
export const sentryEnvironment = {
  dsn: '',
  environment: 'development',
  release: '',
  tracesSampleRate: 0.05,
  replaysOnErrorSampleRate: 1
} as const;
