FROM node:20.17.0-alpine as build
WORKDIR /app
COPY package*.json ./
RUN npm install
RUN npx ngcc --properties es2023 browser module main --first-only --create-ivy-entry-points
COPY . .
RUN npm run build
FROM nginx:stable
COPY default.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/my-stream-history.web/ /usr/share/nginx/html
EXPOSE 80