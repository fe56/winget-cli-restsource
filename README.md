# WinGet REST source

A self-hosted [WinGet REST source](https://github.com/microsoft/winget-cli/blob/master/doc/specs/%23137%20-%20REST%20API.md). Package manifests are stored in SQLite and served to the winget client. The same process hosts a Fluent UI app for browsing and adding packages.

The source URL is the `/api` prefix. Catalog, admin, and health routes sit beside it.

| Project | Role |
|---|---|
| `src/WinGet.RestSource.Server` | ASP.NET host: REST API and Blazor UI |
| `src/WinGet.RestSource` | SQLite store |
| `src/WinGet.RestSource.Utils` | REST models, validation, and YAML import |
| `src/WinGet.RestSource.Server.Tests` | Store and HTTP tests |

`documentation/` holds the WinGet REST OpenAPI descriptions this server implements.

## Run on your machine

From the repository root:

```powershell
dotnet run --project src/WinGet.RestSource.Server
```

| Page | URL |
|---|---|
| Catalog | http://localhost:5141/ |
| Admin | http://localhost:5141/admin |
| Packages | http://localhost:5141/admin/packages |
| API | http://localhost:5141/api |
| Health | http://localhost:5141/health |

The SQLite file is created at `data/winget.db` relative to the working directory. Logs are appended to `logs/winget-restsource.log` in that same directory. Console logging stays on.

Override a setting with an environment variable or `appsettings.json`:

```powershell
$env:ServerIdentifier = "winget-restsource-self-hosted"
$env:Database__Path = "data/winget.db"
$env:Logging__FilePath = "logs/winget-restsource.log"
$env:Logging__LogLevel__Default = "Information"
$env:ApiSettings__ApiKey = ""
dotnet run --project src/WinGet.RestSource.Server
```

Set `Logging__FilePath` to an empty string to keep console logging only. A relative path is resolved from the working directory.

```powershell
dotnet test src/WinGet.RestSource.Server.Tests
```

## Run with Docker

The image listens on port 8080. The database is `/data/winget.db`. The log file is `/logs/winget-restsource.log`.

From `src/WinGet.RestSource.Server`:

```powershell
copy .env.example .env
docker compose up -d --build
```

Compose works without `.env`. Every placeholder has a default. Edit `.env` to change them, then run `docker compose up -d` again.

| Variable | Default | App setting |
|---|---|---|
| `HTTP_PORT` | `8080` | Host port published to the machine |
| `CONTAINER_PORT` | `8080` | `ASPNETCORE_HTTP_PORTS` and `ASPNETCORE_URLS` |
| `SERVER_NAME` | `winget-restsource-self-hosted` | `ServerIdentifier` |
| `DATABASE_PATH` | `/data/winget.db` | `Database:Path` |
| `DATA_DIR` | `winget-data` | Named volume mounted at `/data` |
| `LOG_FILE` | `/logs/winget-restsource.log` | `Logging:FilePath` |
| `LOG_DIR` | `winget-logs` | Named volume mounted at `/logs` |
| `LOG_LEVEL` | `Information` | `Logging:LogLevel:Default` |
| `ASPNET_LOG_LEVEL` | `Warning` | `Logging:LogLevel:Microsoft.AspNetCore` |
| `API_KEY` | empty | `ApiSettings:ApiKey` |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Host environment |

`SERVER_NAME` is the identifier the API returns from `GET /api/information`. The name you pass to `winget source add -n` is chosen on the client and can be different.

`DATA_DIR` and `LOG_DIR` are Docker volume names. Set either one to a host path to bind-mount a directory instead:

```text
DATA_DIR=./data
LOG_DIR=./logs
```

Those paths are relative to `src/WinGet.RestSource.Server`. The log file is append-only and is not rotated.

With the defaults, open:

- Catalog: http://localhost:8080/
- Admin: http://localhost:8080/admin
- API: http://localhost:8080/api
- Health: http://localhost:8080/health

Build and run the image without compose. The build context is `src`:

```powershell
docker build -f src/WinGet.RestSource.Server/Dockerfile -t winget-restsource src
docker run --rm -p 8080:8080 -v winget-data:/data -v winget-logs:/logs winget-restsource
```

Pass `-e ServerIdentifier=my-source` or any other variable from the table to override an image default.

Stop compose with `docker compose down` from `src/WinGet.RestSource.Server`. Add `-v` only when you also want to delete the database and log volumes.

## Use the app

1. Open the catalog to browse packages already in the database.
2. Open **Packages** and choose **Add package** to import a manifest.
3. The dashboard shows the package count, the server identifier, and the command that registers this source with winget.

GET routes are open so winget can read the source. `POST /api/manifestSearch` is also open. POST, PUT, and DELETE on packages, manifests, versions, installers, and locales require the `X-Api-Key` header when `ApiSettings:ApiKey` is set. Leave the key empty on a machine you trust.

`winget source add` needs an elevated terminal. Localhost can use HTTP. A client on another machine needs HTTPS, usually through a reverse proxy in front of this container.

```powershell
winget source add -n local -a http://localhost:5141/api -t Microsoft.Rest
winget search --source local
winget show --source local Sample.YamlApp
```

For the Docker port, use `http://localhost:8080/api`. `winget install` downloads the installer URL in the manifest and checks the SHA256. A placeholder URL such as `https://example.com/sample.exe` will not install.

## Create a package

A package in this source is one manifest document, identified by `PackageIdentifier`. Add it as merged YAML in the admin UI, or as REST JSON through the API. Saving the same identifier again updates that package.

The installer file has to be available at an HTTP or HTTPS URL the winget client can download. Hash the file you publish:

```powershell
Get-FileHash .\MyApp.exe -Algorithm SHA256
```

Use the hash with no spaces. It is 64 hexadecimal characters.

### Merged YAML

Admin → **Packages** → **Add package** opens the YAML tab. Paste a merged manifest or drop a `.yaml` file, then choose **Save YAML**.

Start from `src/WinGet.RestSource.Server/Samples/sample.package.yaml`:

```yaml
PackageIdentifier: Sample.YamlApp
PackageVersion: 1.0.0
PackageLocale: en-US
Publisher: Sample Publisher
PackageName: Sample YAML App
License: MIT
ShortDescription: A sample package imported from YAML
ManifestType: merged
ManifestVersion: 1.6.0
Installers:
- Architecture: x64
  InstallerType: exe
  InstallerUrl: https://example.com/sample.exe
  InstallerSha256: 01234567890ABCDEF01234567890ABCDEF01234567890ABCDEF1234567890ABC
```

Replace `InstallerUrl` and `InstallerSha256` before you install the package. `PackageIdentifier` is `Publisher.Package` style and stays stable across versions. To publish a new version, use the same identifier and a new `PackageVersion`.

[`wingetcreate`](https://github.com/microsoft/winget-create) can collect the URL, hash, and metadata (`wingetcreate new <installer-url>`). It writes separate version, installer, and locale files. Copy those fields into one document with `ManifestType: merged` and import that file here.

### REST JSON

The JSON tab on the same page accepts a `PackageManifest` document. The API accepts the same body.

`POST /api/packageManifests` creates a package. `PUT /api/packageManifests/{PackageIdentifier}` replaces it. Property names are PascalCase.

```json
{
  "PackageIdentifier": "Sample.RestApp",
  "Versions": [
    {
      "PackageVersion": "1.0.0",
      "DefaultLocale": {
        "PackageLocale": "en-US",
        "Publisher": "Sample Publisher",
        "PackageName": "Sample REST App",
        "ShortDescription": "A sample package imported from JSON",
        "License": "MIT"
      },
      "Installers": [
        {
          "InstallerIdentifier": "x64-exe",
          "Architecture": "x64",
          "InstallerType": "exe",
          "InstallerUrl": "https://example.com/sample.exe",
          "InstallerSha256": "01234567890ABCDEF01234567890ABCDEF01234567890ABCDEF1234567890ABC"
        }
      ]
    }
  ]
}
```

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5141/api/packageManifests `
  -ContentType "application/json" `
  -InFile .\manifest.json
```

When an API key is configured, add `-Headers @{ "X-Api-Key" = "your-key" }`.

Confirm the package on the catalog page, then:

```powershell
winget search Sample.RestApp --source local
winget show Sample.RestApp --source local
winget install Sample.RestApp --source local
```

## Settings

| Setting | Default | Environment variable |
|---|---|---|
| `ServerIdentifier` | `winget-restsource-self-hosted` | `ServerIdentifier` |
| `Database:Path` | `data/winget.db` | `Database__Path` |
| `Logging:FilePath` | `logs/winget-restsource.log` | `Logging__FilePath` |
| `Logging:LogLevel:Default` | `Information` | `Logging__LogLevel__Default` |
| `ApiSettings:ApiKey` | empty | `ApiSettings__ApiKey` |

Docker maps the names in the compose table onto these settings and uses the container paths `/data/winget.db` and `/logs/winget-restsource.log`.
