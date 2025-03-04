FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MyStreamHistory.API/MyStreamHistory.API.csproj", "MyStreamHistory.API/"]
RUN dotnet restore "MyStreamHistory.API/MyStreamHistory.API.csproj"
COPY . .
WORKDIR "/src/MyStreamHistory.API"
RUN dotnet build "MyStreamHistory.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MyStreamHistory.API.csproj" -c Release -o /app/publish

FROM nginx:stable
COPY MyStreamHistory.API/default.conf /etc/nginx/conf.d/default.conf
COPY --from=publish /app/publish /usr/share/nginx/html
EXPOSE 80
