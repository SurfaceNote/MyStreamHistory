FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution file
COPY ["MyStreamHistory.Backend.sln", "./"]

# Copy all project files
COPY ["Shared/MyStreamHistory.Shared.Base/MyStreamHistory.Shared.Base.csproj", "Shared/MyStreamHistory.Shared.Base/"]
COPY ["Shared/MyStreamHistory.Shared.Application/MyStreamHistory.Shared.Application.csproj", "Shared/MyStreamHistory.Shared.Application/"]
COPY ["Shared/MyStreamHistory.Shared.Infrastructure/MyStreamHistory.Shared.Infrastructure.csproj", "Shared/MyStreamHistory.Shared.Infrastructure/"]
COPY ["Shared/MyStreamHistory.Shared.Api/MyStreamHistory.Shared.Api.csproj", "Shared/MyStreamHistory.Shared.Api/"]
COPY ["TwitchTrackingService/MyStreamHistory.TwitchTrackingService.Domain/MyStreamHistory.TwitchTrackingService.Domain.csproj", "TwitchTrackingService/MyStreamHistory.TwitchTrackingService.Domain/"]
COPY ["TwitchTrackingService/MyStreamHistory.TwitchTrackingService.Application/MyStreamHistory.TwitchTrackingService.Application.csproj", "TwitchTrackingService/MyStreamHistory.TwitchTrackingService.Application/"]
COPY ["TwitchTrackingService/MyStreamHistory.TwitchTrackingService.Infrastructure/MyStreamHistory.TwitchTrackingService.Infrastructure.csproj", "TwitchTrackingService/MyStreamHistory.TwitchTrackingService.Infrastructure/"]
COPY ["TwitchTrackingService/MyStreamHistory.TwitchTrackingService.Api/MyStreamHistory.TwitchTrackingService.Api.csproj", "TwitchTrackingService/MyStreamHistory.TwitchTrackingService.Api/"]

# Restore dependencies
RUN dotnet restore "TwitchTrackingService/MyStreamHistory.TwitchTrackingService.Api/MyStreamHistory.TwitchTrackingService.Api.csproj"

# Copy everything else
COPY . .

WORKDIR "/src/TwitchTrackingService/MyStreamHistory.TwitchTrackingService.Api"
RUN dotnet build "MyStreamHistory.TwitchTrackingService.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MyStreamHistory.TwitchTrackingService.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyStreamHistory.TwitchTrackingService.Api.dll"]

