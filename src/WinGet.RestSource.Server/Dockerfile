FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first for better layer caching
COPY WinGet.RestSource.slnx ./
COPY WinGet.RestSource.Server/WinGet.RestSource.Server.csproj WinGet.RestSource.Server/
COPY WinGet.RestSource/WinGet.RestSource.csproj WinGet.RestSource/
COPY WinGet.RestSource.Utils/WinGet.RestSource.Utils.csproj WinGet.RestSource.Utils/
COPY NuGet.config ./

# Restore only the Server project and its dependencies
RUN dotnet restore WinGet.RestSource.Server/WinGet.RestSource.Server.csproj

# Copy source files
COPY WinGet.RestSource.Server/ WinGet.RestSource.Server/
COPY WinGet.RestSource/ WinGet.RestSource/
COPY WinGet.RestSource.Utils/ WinGet.RestSource.Utils/

# Build and publish
RUN dotnet publish WinGet.RestSource.Server/WinGet.RestSource.Server.csproj -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create data directory for SQLite
RUN mkdir -p /data

COPY --from=build /app/publish .

# Volume for persistent SQLite database
VOLUME /data

ENV ASPNETCORE_URLS=http://+:8080
ENV Database__Path=/data/winget.db

EXPOSE 8080

# The aspnet image does not include curl. Orchestrators can probe GET /health.

ENTRYPOINT ["dotnet", "WinGet.RestSource.Server.dll"]
