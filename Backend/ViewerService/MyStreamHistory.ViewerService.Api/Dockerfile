FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY ["ViewerService/MyStreamHistory.ViewerService.Api/MyStreamHistory.ViewerService.Api.csproj", "ViewerService/MyStreamHistory.ViewerService.Api/"]
COPY ["ViewerService/MyStreamHistory.ViewerService.Infrastructure/MyStreamHistory.ViewerService.Infrastructure.csproj", "ViewerService/MyStreamHistory.ViewerService.Infrastructure/"]
COPY ["ViewerService/MyStreamHistory.ViewerService.Application/MyStreamHistory.ViewerService.Application.csproj", "ViewerService/MyStreamHistory.ViewerService.Application/"]
COPY ["ViewerService/MyStreamHistory.ViewerService.Domain/MyStreamHistory.ViewerService.Domain.csproj", "ViewerService/MyStreamHistory.ViewerService.Domain/"]
COPY ["Shared/MyStreamHistory.Shared.Api/MyStreamHistory.Shared.Api.csproj", "Shared/MyStreamHistory.Shared.Api/"]
COPY ["Shared/MyStreamHistory.Shared.Infrastructure/MyStreamHistory.Shared.Infrastructure.csproj", "Shared/MyStreamHistory.Shared.Infrastructure/"]
COPY ["Shared/MyStreamHistory.Shared.Application/MyStreamHistory.Shared.Application.csproj", "Shared/MyStreamHistory.Shared.Application/"]
COPY ["Shared/MyStreamHistory.Shared.Base/MyStreamHistory.Shared.Base.csproj", "Shared/MyStreamHistory.Shared.Base/"]

# Restore dependencies
RUN dotnet restore "ViewerService/MyStreamHistory.ViewerService.Api/MyStreamHistory.ViewerService.Api.csproj"

# Copy all source code
COPY . .

# Build
WORKDIR "/src/ViewerService/MyStreamHistory.ViewerService.Api"
RUN dotnet build "MyStreamHistory.ViewerService.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MyStreamHistory.ViewerService.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyStreamHistory.ViewerService.Api.dll"]

