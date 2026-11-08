# syntax=docker/dockerfile:1
FROM node:22-alpine AS ui
WORKDIR /ui
COPY Frontend/package*.json ./
RUN npm ci
COPY Frontend/ ./
RUN npm run build:admin

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Backend/Shared/ Backend/Shared/
COPY Backend/AdminService/MyStreamHistory.AdminService.Api/ Backend/AdminService/MyStreamHistory.AdminService.Api/
RUN dotnet publish Backend/AdminService/MyStreamHistory.AdminService.Api/MyStreamHistory.AdminService.Api.csproj -c Release -o /publish -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
RUN mkdir -p /var/lib/msh-admin/keys && chown -R $APP_UID /var/lib/msh-admin
COPY --from=build /publish/ ./
COPY --from=ui /ui/dist/my-stream-history.admin/browser/ ./wwwroot/
ENV ASPNETCORE_HTTP_PORTS=8080 AdminAuth__DataProtectionKeysPath=/var/lib/msh-admin/keys
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "MyStreamHistory.AdminService.Api.dll"]
