FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ContentService/MyStreamHistory.ContentService.Api/MyStreamHistory.ContentService.Api.csproj ContentService/MyStreamHistory.ContentService.Api/
COPY Shared/MyStreamHistory.Shared.Api/MyStreamHistory.Shared.Api.csproj Shared/MyStreamHistory.Shared.Api/
COPY Shared/MyStreamHistory.Shared.Base/MyStreamHistory.Shared.Base.csproj Shared/MyStreamHistory.Shared.Base/
RUN dotnet restore ContentService/MyStreamHistory.ContentService.Api/MyStreamHistory.ContentService.Api.csproj
COPY . .
RUN dotnet publish ContentService/MyStreamHistory.ContentService.Api/MyStreamHistory.ContentService.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
USER $APP_UID
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "MyStreamHistory.ContentService.Api.dll"]
