// -----------------------------------------------------------------------
// <copyright file="PackageCatalogServiceTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. Licensed under the MIT License.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.WinGet.RestSource.Server.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.WinGet.RestSource.Server.Services;
    using Microsoft.WinGet.RestSource.Sqlite;
    using Microsoft.WinGet.RestSource.Utils.Models.ExtendedSchemas;
    using Microsoft.WinGet.RestSource.Utils.Models.Schemas;

    /// <summary>
    /// Tests for PackageCatalogService.
    /// </summary>
    public class PackageCatalogServiceTests : IDisposable
    {
        private readonly string dbPath;
        private readonly SqliteDataStore store;
        private readonly PackageCatalogService catalog;

        public PackageCatalogServiceTests()
        {
            this.dbPath = Path.Combine(Path.GetTempPath(), $"winget_catalog_{Guid.NewGuid():N}.db");
            this.store = new SqliteDataStore(NullLogger<SqliteDataStore>.Instance, this.dbPath);
            this.catalog = new PackageCatalogService(this.store);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            try { File.Delete(this.dbPath); } catch { }
            try { File.Delete(this.dbPath + "-wal"); } catch { }
            try { File.Delete(this.dbPath + "-shm"); } catch { }
        }

        [Fact]
        public async Task AddFromYaml_CreatesManifest()
        {
            var yaml = """
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
                """;

            var result = await this.catalog.AddFromYamlAsync(yaml);

            Assert.Equal("Sample.YamlApp", result.PackageIdentifier);
            var loaded = await this.catalog.GetAsync("Sample.YamlApp");
            Assert.NotNull(loaded);
            Assert.Equal("Sample YAML App", loaded!.Versions[0].DefaultLocale.PackageName);
        }

        [Fact]
        public async Task GetAllManifests_FollowsPagination()
        {
            for (int i = 0; i < 25; i++)
            {
                await this.store.AddPackageManifest(CreateTestManifest($"Pub.App{i:D3}"));
            }

            var all = await this.catalog.GetAllManifestsAsync();
            Assert.Equal(25, all.Count);
        }

        [Fact]
        public async Task ConcurrentAdds_DoNotLoseRows()
        {
            var tasks = Enumerable.Range(0, 12)
                .Select(i => this.store.AddPackageManifest(CreateTestManifest($"Pub.Concurrent{i:D2}")));

            await Task.WhenAll(tasks);
            Assert.Equal(12, await this.store.Count());
        }

        private static PackageManifest CreateTestManifest(string id)
        {
            return new PackageManifest
            {
                PackageIdentifier = id,
                Versions = new VersionsExtended
                {
                    new VersionExtended
                    {
                        PackageVersion = "1.0.0",
                        DefaultLocale = new DefaultLocale
                        {
                            PackageLocale = "en-US",
                            Publisher = "Test Publisher",
                            PackageName = "Test App",
                            ShortDescription = "A test application",
                            License = "MIT",
                        },
                        Installers = new Installers
                        {
                            new Utils.Models.Schemas.Installer
                            {
                                InstallerIdentifier = "x64-exe",
                                Architecture = "x64",
                                InstallerType = "exe",
                                InstallerUrl = "https://example.com/test.exe",
                                InstallerSha256 = "A000000000000000000000000000000000000000000000000000000000000000",
                            },
                        },
                    },
                },
            };
        }
    }
}
