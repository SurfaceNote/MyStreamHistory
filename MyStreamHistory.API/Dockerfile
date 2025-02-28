# Установка базового образа для ASP.NET
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Образ для сборки проекта с .NET SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MyStreamHistory.API/MyStreamHistory.API.csproj", "MyStreamHistory.API/"]
RUN dotnet restore "MyStreamHistory.API/MyStreamHistory.API.csproj"
COPY . .
WORKDIR "/src/MyStreamHistory.API"
RUN dotnet build "MyStreamHistory.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MyStreamHistory.API.csproj" -c Release -o /app/publish

# Окончательная сборка и подготовка приложения
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyStreamHistory.API.dll"]

# Установка дополнительных пакетов (если необходимо)
#RUN apt-get update && apt-get install -y \
    #curl \
    #&& curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel LTS --install-dir /usr/local/share/dotnet \
    #&& ln -s /usr/local/share/dotnet/dotnet /usr/bin/dotnet
#