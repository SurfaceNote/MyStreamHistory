FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MyStreamHistory.API/MyStreamHistory.API.csproj", "MyStreamHistory.API/"]
RUN dotnet restore "MyStreamHistory.API/MyStreamHistory.API.csproj"
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"
COPY . . 
WORKDIR "/src/MyStreamHistory.API"
RUN dotnet build "MyStreamHistory.API.csproj" -c Release -o /app/build
RUN dotnet publish "MyStreamHistory.API.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish . 

RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"
ENTRYPOINT ["dotnet", "MyStreamHistory.API.dll"]
