FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first for better layer caching
COPY WinGet.RestSource.slnx ./
COPY WinGet.RestSource.Server/WinGet.RestSource.Server.csproj WinGet.RestSource.Server/
COPY WinGet.RestSource/WinGet.RestSource.csproj WinGet.RestSource/
COPY WinGet.RestSource.Utils/WinGet.RestSource.Utils.csproj WinGet.RestSource.Utils/
COPY NuGet.config ./

RUN dotnet restore WinGet.RestSource.Server/WinGet.RestSource.Server.csproj

COPY WinGet.RestSource.Server/ WinGet.RestSource.Server/
COPY WinGet.RestSource/ WinGet.RestSource/
COPY WinGet.RestSource.Utils/ WinGet.RestSource.Utils/

RUN dotnet publish WinGet.RestSource.Server/WinGet.RestSource.Server.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The runtime image defaults to a non-root user. Stay root so a mounted
# data or log directory is writable.
USER root
RUN mkdir -p /data /logs

COPY --from=build /app/publish .

VOLUME ["/data", "/logs"]

# Runtime defaults. docker compose and `docker run -e` override them.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_URLS=http://+:8080 \
    ServerIdentifier=winget-restsource-self-hosted \
    Database__Path=/data/winget.db \
    Logging__FilePath=/logs/winget-restsource.log \
    Logging__LogLevel__Default=Information \
    ApiSettings__ApiKey=

EXPOSE 8080

ENTRYPOINT ["dotnet", "WinGet.RestSource.Server.dll"]
