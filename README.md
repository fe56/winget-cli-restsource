# WinGet REST source

A self-hosted [WinGet REST source](https://github.com/microsoft/winget-cli/blob/master/doc/specs/%23137%20-%20REST%20API.md). It stores package manifests in SQLite and serves them to the winget client. A Fluent UI Blazor app is included for browsing and adding packages.

## Projects

| Project | Role |
|---|---|
| `src/WinGet.RestSource.Server` | ASP.NET host: REST API and Blazor UI |
| `src/WinGet.RestSource` | `SqliteDataStore` |
| `src/WinGet.RestSource.Utils` | REST models, validation, and YAML import |
| `src/WinGet.RestSource.Server.Tests` | Store and HTTP tests |

`documentation/` holds the WinGet REST OpenAPI descriptions this server implements.

## Run

```powershell
dotnet run --project src/WinGet.RestSource.Server
```

- Catalog: http://localhost:5141/
- Admin: http://localhost:5141/admin
- API: http://localhost:5141/api
- Health: http://localhost:5141/health

```powershell
dotnet test src/WinGet.RestSource.Server.Tests
```

Docker (from `src/WinGet.RestSource.Server`):

```bash
docker compose up -d
```

The container listens on http://localhost:8080. The database is `/data/winget.db`.

## Use with winget

Add a package in **Packages** (merged YAML or REST JSON). A sample manifest is in `src/WinGet.RestSource.Server/Samples/sample.package.yaml`.

Then, from an elevated terminal:

```powershell
winget source add -n local -a http://localhost:5141/api -t Microsoft.Rest
winget search --source local
winget show --source local Sample.YamlApp
```

`winget install` succeeds only when the installer URL and SHA256 are real. Localhost can use HTTP. A remote host needs HTTPS.

GET routes are open so winget can read the source. POST, PUT, and DELETE require the `X-Api-Key` header when `ApiSettings:ApiKey` is set. Leave it empty for local use.

| Setting | Default |
|---|---|
| `ApiSettings:ApiKey` | empty |
| `Database:Path` | `data/winget.db` |
| `ServerIdentifier` | `winget-restsource-self-hosted` |
