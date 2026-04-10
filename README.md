# Welcome to the winget-cli-restsource repository

## Self-Hosted WinGet REST Source (Docker / SQLite)

This fork adds a **self-hostable** WinGet REST source that runs in a Docker container with SQLite — no Azure dependencies required.

### Quick Start (Docker)

```bash
# Clone the repository
git clone https://github.com/user/winget-cli-restsource.git
cd winget-cli-restsource/src/WinGet.RestSource.Server

# Copy and edit environment config
cp .env.example .env
# Edit .env to set your API key:
#   WINGET_API_KEY=your-secret-key-here

# Start with Docker Compose
docker compose up -d
```

The server starts on **http://localhost:8080**:
- **REST API**: `http://localhost:8080/api/` — winget client compatible
- **Admin UI**: `http://localhost:8080/admin` — Blazor FluentUI management dashboard

### Add as a winget source

```powershell
winget source add -n "my-private-repo" -a http://localhost:8080/api/ -t "Microsoft.Rest"
```

### Configuration

Configuration via environment variables or `appsettings.json`:

| Variable | Default | Description |
|---|---|---|
| `ApiSettings__ApiKey` | *(empty)* | API key for write operations (POST/PUT/DELETE). If empty, writes are unrestricted. |
| `Database__Path` | `data/winget.db` | Path to SQLite database file |
| `ServerIdentifier` | `winget-restsource-self-hosted` | Server identifier returned by `/api/information` |

### Data Persistence

The SQLite database is stored at `/data/winget.db` inside the container. Mount a volume to persist data:

```yaml
volumes:
  - winget-data:/data
```

### API Authentication

- **GET** routes are open (no authentication) — winget clients can read without credentials
- **POST/PUT/DELETE** routes require an `X-Api-Key` header matching the configured API key

```bash
# Add a package manifest
curl -X POST http://localhost:8080/api/packageManifests \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: your-secret-key-here" \
  -d @manifest.json
```

### Running Without Docker

```bash
cd src/WinGet.RestSource.Server
dotnet run
```

The server starts on `http://localhost:5141` by default (configurable in `Properties/launchSettings.json`).

### Admin Dashboard

The built-in Blazor Server UI at `/admin` provides:
- **Dashboard** — Package count overview
- **Package List** — Searchable/filterable grid of all packages
- **Package Detail** — View versions, installers, locales; edit via JSON

---

## Azure Deployment (Original)

The original Azure-based deployment using Azure Functions and Cosmos DB is still available in the `WinGet.RestSource.Functions` project.

### Create a Windows Package Manager REST source on Azure with PowerShell

The [Microsoft.WinGet.RestSource](https://www.powershellgallery.com/packages/Microsoft.WinGet.RestSource) PowerShell module is provided for creating and managing Windows Package Manager REST sources.

Please visit [Create a Windows Package Manager REST source](/Tools/PowershellModule/doc/WingetRestSource.md) for more details.

## Building the client

### Prerequisites

* [Git Large File Storage (LFS)](https://git-lfs.github.com/)
* [Visual Studio 2022](https://visualstudio.microsoft.com/downloads/)
* The following workloads:
   * .NET desktop development
   * Azure development (only needed for Azure Functions project)
   * ASP<area>.NET and web development

Open `src\WinGet.RestSource.sln` in Visual Studio and build. We currently only build using the solution; command line methods of building a VS solution should work as well.

## Running locally

### Self-Hosted Server (Recommended)

```bash
cd src/WinGet.RestSource.Server
dotnet run
```

Then add the source: `winget source add -n "local" -a http://localhost:5141/api/ -t "Microsoft.Rest"`

### Azure Functions (Legacy)

The REST functions can be run locally, but to use winget with them, the functions must be run using HTTPS. This is pre-configured by the `launchSettings.json` file.

1. In the `src\WinGet.RestSource.Functions` directory, run `generate_self_sign_cert.ps1` in PowerShell.
   * This will generate a test pfx and install it into the Root store.
   * It will automatically be used as the HTTPS cert during local execution, thanks to `launchSettings.json`
2. Create a CosmosDB database instance in Azure, using either the above instructions, or [manually](https://docs.microsoft.com/azure/cosmos-db/sql/create-cosmosdb-resources-portal).
   * Navigate to the Keys section of your CosmosDB instance in the Azure portal to find your connection information.
   * If you've used the ARM templates as described above, your Database will be named `WinGet` and your Collection will be `Manifests`
3. Copy `src\WinGet.RestSource.Functions\local.settings.template.json` to `local.settings.json` and populate required fields from the above Keys section.
4. In source codes, change HttpTrigger level to Anonymous for InformationGet, ManifestSearchPost and ManifestGet endpoints.
5. Run the `WinGet.RestSource.Functions` project locally in Visual Studio using F5.
6. Add it as a source in winget with: `winget source add -n "winget-pkgs-restsource" -a https://localhost:7071/api/ -t "Microsoft.Rest"`

Your commands to winget will now use your locally running REST instance as a user added source.

## Running Tests

Running tests are a great way to ensure that functionality is preserved across major changes. You can run these tests in Visual Studio Test Explorer. In Visual Studio, run the tests from the menu with Test > Run All Tests

### Unit Testing Prerequisites

* Install the [Cosmos DB Emulator](https://docs.microsoft.com/azure/cosmos-db/local-emulator?tabs=ssl-netstd21)
* Copy the `WinGet.RestSource.UnitTest\Test.runsettings.template.json` template configuration to `Test.runsettings.json`
  * The defaults should work for your local Cosmos DB emulator instance. You can change the configuration to point to a Cosmos DB instance in Azure instead.
  * Alternatively, all of the test configuration properties can be set as environment variables. This is useful for overriding properties in an ADO build.

### Integration Testing Prerequisites

* Install the [winget client](https://github.com/microsoft/winget-cli) locally.
* Copy the `WinGet.RestSource.IntegrationTest\Test.runsettings.template.json` template configuration to `Test.runsettings.json`
  * Modify the `RestSourceUrl` property to point to a deployed REST source endpoint. You can use the below instructions to deploy a REST instance.
  * If the local winget client doesn't already have the source added, the integration tests can add it. To do so, change the `AddRestSource` property to true. Visual Studio must be running as admin in this case.
  * There is a test case that **modifies** the REST source. This test is disabled by default. To run it the `RunWriteTests` setting must be set to true. The `FunctionsHostKey` setting must also be set since the add/update/delete endpoints all require function authorization. We recommend creating a new pipeline-specific host key for this purpose.

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
