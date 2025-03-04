FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MyStreamHistory.API/MyStreamHistory.API.csproj", "MyStreamHistory.API/"]
RUN dotnet restore "MyStreamHistory.API/MyStreamHistory.API.csproj"
COPY . .
WORKDIR "/src/MyStreamHistory.API"
RUN dotnet build "MyStreamHistory.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MyStreamHistory.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "MyStreamHistory.API.dll", "--migrate"]
