// -----------------------------------------------------------------------
// <copyright file="SqliteDataStoreTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. Licensed under the MIT License.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.WinGet.RestSource.Server.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.WinGet.RestSource.Sqlite;
    using Microsoft.WinGet.RestSource.Utils.Models.ExtendedSchemas;
    using Microsoft.WinGet.RestSource.Utils.Models.Objects;
    using Microsoft.WinGet.RestSource.Utils.Models.Schemas;
    using Xunit;
    using Version = Microsoft.WinGet.RestSource.Utils.Models.Schemas.Version;

    /// <summary>
    /// Tests for SqliteDataStore.
    /// Each test uses a unique temp database file for isolation.
    /// </summary>
    public class SqliteDataStoreTests : IDisposable
    {
        private readonly string dbPath;
        private readonly SqliteDataStore store;

        public SqliteDataStoreTests()
        {
            this.dbPath = Path.Combine(Path.GetTempPath(), $"winget_test_{Guid.NewGuid():N}.db");
            this.store = new SqliteDataStore(NullLogger<SqliteDataStore>.Instance, this.dbPath);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            try { File.Delete(this.dbPath); } catch { }
            try { File.Delete(this.dbPath + "-wal"); } catch { }
            try { File.Delete(this.dbPath + "-shm"); } catch { }
        }

        private static PackageManifest CreateTestManifest(string id = "TestPublisher.TestApp", string version = "1.0.0")
        {
            return new PackageManifest
            {
                PackageIdentifier = id,
                Versions = new VersionsExtended
                {
                    new VersionExtended
                    {
                        PackageVersion = version,
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
                            new Installer
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

        // ---- Count ----

        [Fact]
        public async Task Count_EmptyDatabase_ReturnsZero()
        {
            var count = await this.store.Count();
            Assert.Equal(0, count);
        }

        [Fact]
        public async Task Count_AfterAddingPackages_ReturnsCorrectCount()
        {
            await this.store.AddPackageManifest(CreateTestManifest("Pub.App1"));
            await this.store.AddPackageManifest(CreateTestManifest("Pub.App2"));

            Assert.Equal(2, await this.store.Count());
        }

        // ---- Package CRUD ----

        [Fact]
        public async Task AddPackage_AndRetrieve_Succeeds()
        {
            var manifest = CreateTestManifest();
            await this.store.AddPackageManifest(manifest);

            var result = await this.store.GetPackages("TestPublisher.TestApp", null);
            Assert.Single(result.Items);
            Assert.Equal("TestPublisher.TestApp", result.Items[0].PackageIdentifier);
        }

        [Fact]
        public async Task AddPackage_Duplicate_Throws()
        {
            var manifest = CreateTestManifest();
            await this.store.AddPackageManifest(manifest);

            await Assert.ThrowsAnyAsync<Exception>(() =>
                this.store.AddPackageManifest(CreateTestManifest()));
        }

        [Fact]
        public async Task DeletePackage_RemovesPackage()
        {
            await this.store.AddPackageManifest(CreateTestManifest());
            await this.store.DeletePackage("TestPublisher.TestApp");

            Assert.Equal(0, await this.store.Count());
        }

        [Fact]
        public async Task DeletePackage_NonExistent_Throws()
        {
            await Assert.ThrowsAnyAsync<Exception>(() =>
                this.store.DeletePackage("NonExistent.Package"));
        }

        // ---- GetPackages ----

        [Fact]
        public async Task GetPackages_AllPackages_ReturnsAll()
        {
            await this.store.AddPackageManifest(CreateTestManifest("Pub.A"));
            await this.store.AddPackageManifest(CreateTestManifest("Pub.B"));
            await this.store.AddPackageManifest(CreateTestManifest("Pub.C"));

            var result = await this.store.GetPackages(null, null);
            Assert.Equal(3, result.Items.Count);
        }

        [Fact]
        public async Task GetPackages_ByIdentifier_ReturnsSingle()
        {
            await this.store.AddPackageManifest(CreateTestManifest("Pub.A"));
            await this.store.AddPackageManifest(CreateTestManifest("Pub.B"));

            var result = await this.store.GetPackages("Pub.A", null);
            Assert.Single(result.Items);
            Assert.Equal("Pub.A", result.Items[0].PackageIdentifier);
        }

        [Fact]
        public async Task GetPackages_NonExistent_ReturnsEmpty()
        {
            var result = await this.store.GetPackages("Missing.App", null);
            Assert.Empty(result.Items);
        }

        // ---- PackageManifest CRUD ----

        [Fact]
        public async Task GetPackageManifests_ReturnsFullManifest()
        {
            await this.store.AddPackageManifest(CreateTestManifest());

            var result = await this.store.GetPackageManifests("TestPublisher.TestApp");
            Assert.Single(result.Items);

            var manifest = result.Items[0];
            Assert.Equal("TestPublisher.TestApp", manifest.PackageIdentifier);
            Assert.NotNull(manifest.Versions);
            Assert.Single(manifest.Versions);
            Assert.Equal("1.0.0", manifest.Versions[0].PackageVersion);
        }

        [Fact]
        public async Task UpdatePackageManifest_UpdatesData()
        {
            await this.store.AddPackageManifest(CreateTestManifest());

            var updated = CreateTestManifest();
            updated.Versions[0].DefaultLocale.PackageName = "Updated App Name";
            await this.store.UpdatePackageManifest("TestPublisher.TestApp", updated);

            var result = await this.store.GetPackageManifests("TestPublisher.TestApp");
            Assert.Equal("Updated App Name", result.Items[0].Versions[0].DefaultLocale.PackageName);
        }

        // ---- Version CRUD ----

        [Fact]
        public async Task AddVersion_AddsToExistingPackage()
        {
            await this.store.AddPackageManifest(CreateTestManifest());

            var newVersion = new VersionExtended
            {
                PackageVersion = "2.0.0",
                DefaultLocale = new DefaultLocale
                {
                    PackageLocale = "en-US",
                    Publisher = "Test Publisher",
                    PackageName = "Test App",
                    ShortDescription = "Version 2",
                    License = "MIT",
                },
                Installers = new Installers
                {
                    new Installer
                    {
                        InstallerIdentifier = "x64-exe-v2",
                        Architecture = "x64",
                        InstallerType = "exe",
                        InstallerUrl = "https://example.com/test2.exe",
                        InstallerSha256 = "B000000000000000000000000000000000000000000000000000000000000000",
                    },
                },
            };

            await this.store.AddVersion("TestPublisher.TestApp", newVersion);

            var result = await this.store.GetPackageManifests("TestPublisher.TestApp");
            Assert.Equal(2, result.Items[0].Versions.Count);
        }

        [Fact]
        public async Task DeleteVersion_RemovesVersion()
        {
            await this.store.AddPackageManifest(CreateTestManifest());
            await this.store.DeleteVersion("TestPublisher.TestApp", "1.0.0");

            // Deleting the only version should delete the whole package
            Assert.Equal(0, await this.store.Count());
        }

        [Fact]
        public async Task GetVersions_ReturnsVersionList()
        {
            await this.store.AddPackageManifest(CreateTestManifest());

            var result = await this.store.GetVersions("TestPublisher.TestApp", null);
            Assert.Single(result.Items);
            Assert.Equal("1.0.0", result.Items[0].PackageVersion);
        }

        // ---- Installer CRUD ----

        [Fact]
        public async Task GetInstallers_ReturnsInstallers()
        {
            await this.store.AddPackageManifest(CreateTestManifest());

            var result = await this.store.GetInstallers("TestPublisher.TestApp", "1.0.0", null);
            Assert.Single(result.Items);
            Assert.Equal("x64-exe", result.Items[0].InstallerIdentifier);
        }

        // ---- Locale CRUD ----

        [Fact]
        public async Task AddLocale_AddsAdditionalLocale()
        {
            await this.store.AddPackageManifest(CreateTestManifest());

            var locale = new Locale
            {
                PackageLocale = "de-DE",
                Publisher = "Test Herausgeber",
                PackageName = "Test Anwendung",
                ShortDescription = "Eine Testanwendung",
                License = "MIT",
            };

            await this.store.AddLocale("TestPublisher.TestApp", "1.0.0", locale);

            var result = await this.store.GetLocales("TestPublisher.TestApp", "1.0.0", "de-DE");
            Assert.Single(result.Items);
            Assert.Equal("de-DE", result.Items[0].PackageLocale);
        }

        // ---- Search ----

        [Fact]
        public async Task SearchPackageManifests_EmptyQuery_ReturnsAll()
        {
            await this.store.AddPackageManifest(CreateTestManifest("Pub.A"));
            await this.store.AddPackageManifest(CreateTestManifest("Pub.B"));

            var result = await this.store.SearchPackageManifests(null);
            Assert.Equal(2, result.Items.Count);
        }

        [Fact]
        public async Task SearchPackageManifests_ByKeyword_ReturnsMatches()
        {
            await this.store.AddPackageManifest(CreateTestManifest("Pub.Alpha"));
            await this.store.AddPackageManifest(CreateTestManifest("Pub.Beta"));

            var query = new ManifestSearchRequest
            {
                Query = new SearchRequestMatch
                {
                    KeyWord = "Alpha",
                    MatchType = "Substring",
                },
            };

            var result = await this.store.SearchPackageManifests(query);
            Assert.Single(result.Items);
            Assert.Equal("Pub.Alpha", result.Items[0].PackageIdentifier);
        }

        [Fact]
        public async Task SearchPackageManifests_ExactMatch_ReturnsOnlyExact()
        {
            await this.store.AddPackageManifest(CreateTestManifest("Pub.App"));
            await this.store.AddPackageManifest(CreateTestManifest("Pub.Application"));

            var query = new ManifestSearchRequest
            {
                Query = new SearchRequestMatch
                {
                    KeyWord = "Pub.App",
                    MatchType = "Exact",
                },
            };

            var result = await this.store.SearchPackageManifests(query);
            Assert.Single(result.Items);
            Assert.Equal("Pub.App", result.Items[0].PackageIdentifier);
        }

        [Fact]
        public async Task SearchPackageManifests_CaseInsensitive_ReturnsMatches()
        {
            await this.store.AddPackageManifest(CreateTestManifest("Pub.MyApp"));

            var query = new ManifestSearchRequest
            {
                Query = new SearchRequestMatch
                {
                    KeyWord = "pub.myapp",
                    MatchType = "CaseInsensitive",
                },
            };

            var result = await this.store.SearchPackageManifests(query);
            Assert.Single(result.Items);
        }

        [Fact]
        public async Task SearchPackageManifests_NoMatch_ReturnsEmpty()
        {
            await this.store.AddPackageManifest(CreateTestManifest("Pub.App"));

            var query = new ManifestSearchRequest
            {
                Query = new SearchRequestMatch
                {
                    KeyWord = "NonExistent",
                    MatchType = "Substring",
                },
            };

            var result = await this.store.SearchPackageManifests(query);
            Assert.Empty(result.Items);
        }

        // ---- Pagination ----

        [Fact]
        public async Task GetPackages_Pagination_WorksCorrectly()
        {
            // Add many packages to test pagination
            for (int i = 0; i < 5; i++)
            {
                await this.store.AddPackageManifest(CreateTestManifest($"Pub.App{i:D3}"));
            }

            var page1 = await this.store.GetPackages(null, null);
            Assert.True(page1.Items.Count > 0);
            Assert.Equal(5, await this.store.Count());
        }
    }
}
