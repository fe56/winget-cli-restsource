FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for better layer caching
COPY WinGet.RestSource.sln ./
COPY WinGet.RestSource.Server/WinGet.RestSource.Server.csproj WinGet.RestSource.Server/
COPY WinGet.RestSource/WinGet.RestSource.csproj WinGet.RestSource/
COPY WinGet.RestSource.Utils/WinGet.RestSource.Utils.csproj WinGet.RestSource.Utils/
COPY WinGet.RestSource.AppConfig/WinGet.RestSource.AppConfig.csproj WinGet.RestSource.AppConfig/
COPY Microsoft.WindowsPackageManager.Rest/Microsoft.WindowsPackageManager.Rest.csproj Microsoft.WindowsPackageManager.Rest/
COPY NuGet.config ./

# Restore only the Server project and its dependencies
RUN dotnet restore WinGet.RestSource.Server/WinGet.RestSource.Server.csproj

# Copy source files
COPY WinGet.RestSource.Server/ WinGet.RestSource.Server/
COPY WinGet.RestSource/ WinGet.RestSource/
COPY WinGet.RestSource.Utils/ WinGet.RestSource.Utils/
COPY WinGet.RestSource.AppConfig/ WinGet.RestSource.AppConfig/
COPY Microsoft.WindowsPackageManager.Rest/ Microsoft.WindowsPackageManager.Rest/

# Build and publish
RUN dotnet publish WinGet.RestSource.Server/WinGet.RestSource.Server.csproj -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create data directory for SQLite
RUN mkdir -p /data

COPY --from=build /app/publish .

# Volume for persistent SQLite database
VOLUME /data

ENV ASPNETCORE_URLS=http://+:8080
ENV Database__Path=/data/winget.db

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:8080/api/information || exit 1

ENTRYPOINT ["dotnet", "WinGet.RestSource.Server.dll"]
