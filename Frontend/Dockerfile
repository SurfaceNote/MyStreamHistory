# syntax=docker/dockerfile:1.6

FROM node:20.17.0-alpine AS build
WORKDIR /app

ARG SENTRY_DSN=""
ARG SENTRY_ENVIRONMENT="production"
ARG SENTRY_RELEASE=""
ARG SENTRY_TRACES_SAMPLE_RATE="0.05"
ARG SENTRY_REPLAYS_ON_ERROR_SAMPLE_RATE="1"
ARG SENTRY_ORG=""
ARG SENTRY_PROJECT=""

ENV SENTRY_DSN=$SENTRY_DSN \
    SENTRY_ENVIRONMENT=$SENTRY_ENVIRONMENT \
    SENTRY_RELEASE=$SENTRY_RELEASE \
    SENTRY_TRACES_SAMPLE_RATE=$SENTRY_TRACES_SAMPLE_RATE \
    SENTRY_REPLAYS_ON_ERROR_SAMPLE_RATE=$SENTRY_REPLAYS_ON_ERROR_SAMPLE_RATE \
    SENTRY_ORG=$SENTRY_ORG \
    SENTRY_PROJECT=$SENTRY_PROJECT

COPY package*.json ./
RUN npm ci

COPY . .

RUN (npx ngcc --properties es2023 browser module main --first-only --create-ivy-entry-points || true)

RUN npm run configure:sentry && npm run build

RUN --mount=type=secret,id=sentry_auth_token,required=false \
    set -eu; \
    artifacts="/app/dist/my-stream-history.web/browser"; \
    if [ -s /run/secrets/sentry_auth_token ] && [ -n "$SENTRY_ORG" ] && [ -n "$SENTRY_PROJECT" ] && [ -n "$SENTRY_RELEASE" ]; then \
      export SENTRY_AUTH_TOKEN="$(cat /run/secrets/sentry_auth_token)"; \
      if ! npx sentry-cli releases info "$SENTRY_RELEASE" >/dev/null 2>&1; then \
        npx sentry-cli releases new "$SENTRY_RELEASE"; \
      fi; \
      npx sentry-cli sourcemaps inject "$artifacts"; \
      npx sentry-cli sourcemaps upload \
        --org "$SENTRY_ORG" \
        --project "$SENTRY_PROJECT" \
        --release "$SENTRY_RELEASE" \
        --url-prefix "~/" \
        --validate \
        "$artifacts"; \
      npx sentry-cli releases finalize "$SENTRY_RELEASE"; \
    else \
      echo "Sentry source map upload skipped: CI credentials are not configured."; \
    fi; \
    find "$artifacts" -type f -name '*.map' -delete

FROM nginx:stable-alpine

COPY nginx/default.conf /etc/nginx/conf.d/default.conf

COPY --from=build /app/dist/my-stream-history.web/browser/ /usr/share/nginx/html

EXPOSE 80 443
CMD ["nginx", "-g", "daemon off;"]
