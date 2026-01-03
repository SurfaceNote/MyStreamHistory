FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Gateway/MyStreamHistory.Gateway.Api/MyStreamHistory.Gateway.Api.csproj", "Gateway/MyStreamHistory.Gateway.Api/"]
COPY ["Shared/MyStreamHistory.Shared.Api/MyStreamHistory.Shared.Api.csproj", "Shared/MyStreamHistory.Shared.Api/"]
COPY ["Shared/MyStreamHistory.Shared.Base/MyStreamHistory.Shared.Base.csproj", "Shared/MyStreamHistory.Shared.Base/"]
COPY ["Shared/MyStreamHistory.Shared.Infrastructure/MyStreamHistory.Shared.Infrastructure.csproj", "Shared/MyStreamHistory.Shared.Infrastructure/"]
COPY ["Shared/MyStreamHistory.Shared.Application/MyStreamHistory.Shared.Application.csproj", "Shared/MyStreamHistory.Shared.Application/"]
COPY ["Gateway/MyStreamHistory.Gateway.Application/MyStreamHistory.Gateway.Application.csproj", "Gateway/MyStreamHistory.Gateway.Application/"]
RUN dotnet restore "Gateway/MyStreamHistory.Gateway.Api/MyStreamHistory.Gateway.Api.csproj"
COPY . .
WORKDIR "/src/Gateway/MyStreamHistory.Gateway.Api"
RUN dotnet build "./MyStreamHistory.Gateway.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MyStreamHistory.Gateway.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyStreamHistory.Gateway.Api.dll"]
