# syntax=docker/dockerfile:1.6

FROM node:20.17.0-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .

RUN (npx ngcc --properties es2023 browser module main --first-only --create-ivy-entry-points || true)

RUN npm run build

FROM nginx:stable-alpine

COPY nginx/default.conf /etc/nginx/conf.d/default.conf

COPY --from=build /app/dist/my-stream-history.web/browser/ /usr/share/nginx/html

EXPOSE 80 443
CMD ["nginx", "-g", "daemon off;"]
