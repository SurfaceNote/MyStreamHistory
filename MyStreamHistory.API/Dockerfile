FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MyStreamHistory.API/MyStreamHistory.API.csproj", "MyStreamHistory.API/"]
RUN dotnet restore "MyStreamHistory.API/MyStreamHistory.API.csproj"
COPY . . 
WORKDIR "/src/MyStreamHistory.API"
RUN dotnet build "MyStreamHistory.API.csproj" -c Release -o /app/build
RUN dotnet publish "MyStreamHistory.API.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish . 

RUN apt-get update && apt-get install -y wget
RUN wget https://download.visualstudio.microsoft.com/download/pr/8fd8797e-7f2a-43a5-927d-d997e0b3d13e/1bbfa0f94a34cc6fa01a4209bc52c10d/dotnet-sdk-8.0.100-linux-x64.tar.gz
RUN mkdir -p /usr/share/dotnet
RUN tar -xvf dotnet-sdk-8.0.100-linux-x64.tar.gz -C /usr/share/dotnet
RUN ln -s /usr/share/dotnet/dotnet /usr/local/bin/dotnet

RUN dotnet tool install --global dotnet-ef

ENTRYPOINT ["dotnet", "MyStreamHistory.API.dll"]
