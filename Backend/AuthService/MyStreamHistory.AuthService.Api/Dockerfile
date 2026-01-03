FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AuthService/MyStreamHistory.AuthService.Api/MyStreamHistory.AuthService.Api.csproj", "AuthService/MyStreamHistory.AuthService.Api/"]
COPY ["Shared/MyStreamHistory.Shared.Api/MyStreamHistory.Shared.Api.csproj", "Shared/MyStreamHistory.Shared.Api/"]
COPY ["Shared/MyStreamHistory.Shared.Base/MyStreamHistory.Shared.Base.csproj", "Shared/MyStreamHistory.Shared.Base/"]
COPY ["Shared/MyStreamHistory.Shared.Infrastructure/MyStreamHistory.Shared.Infrastructure.csproj", "Shared/MyStreamHistory.Shared.Infrastructure/"]
COPY ["Shared/MyStreamHistory.Shared.Application/MyStreamHistory.Shared.Application.csproj", "Shared/MyStreamHistory.Shared.Application/"]
COPY ["AuthService/MyStreamHistory.AuthService.Infrastructure/MyStreamHistory.AuthService.Infrastructure.csproj", "AuthService/MyStreamHistory.AuthService.Infrastructure/"]
COPY ["AuthService/MyStreamHistory.AuthService.Domain/MyStreamHistory.AuthService.Domain.csproj", "AuthService/MyStreamHistory.AuthService.Domain/"]
COPY ["AuthService/MyStreamHistory.AuthService.Application/MyStreamHistory.AuthService.Application.csproj", "AuthService/MyStreamHistory.AuthService.Application/"]
RUN dotnet restore "AuthService/MyStreamHistory.AuthService.Api/MyStreamHistory.AuthService.Api.csproj"
COPY . .
WORKDIR "/src/AuthService/MyStreamHistory.AuthService.Api"
RUN dotnet build "./MyStreamHistory.AuthService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MyStreamHistory.AuthService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyStreamHistory.AuthService.Api.dll"]
