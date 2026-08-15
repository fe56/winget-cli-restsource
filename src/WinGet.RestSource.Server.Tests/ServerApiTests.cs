// -----------------------------------------------------------------------
// <copyright file="ServerApiTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. Licensed under the MIT License.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.WinGet.RestSource.Server.Tests
{
    using System.Net;
    using System.Net.Http.Headers;
    using System.Text;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.WinGet.RestSource.Utils.Models;
    using Microsoft.WinGet.RestSource.Utils.Models.ExtendedSchemas;
    using Microsoft.WinGet.RestSource.Utils.Models.Schemas;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// HTTP contract tests for the self-hosted REST source.
    /// </summary>
    public class ServerApiTests : IClassFixture<ServerApiFactory>, IDisposable
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new DefaultContractResolver(),
        };

        private readonly ServerApiFactory factory;
        private readonly HttpClient client;

        public ServerApiTests(ServerApiFactory factory)
        {
            this.factory = factory;
            this.client = factory.CreateClient();
        }

        public void Dispose()
        {
            this.client.Dispose();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task Health_ReturnsOk()
        {
            var response = await this.client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Information_ReturnsSourceIdentifierAndVersions()
        {
            var response = await this.client.GetAsync("/api/information");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadAsStringAsync();
            var parsed = JsonConvert.DeserializeObject<ApiResponse<Information>>(json, JsonSettings);
            Assert.NotNull(parsed);
            Assert.False(string.IsNullOrWhiteSpace(parsed!.Data.SourceIdentifier));
            Assert.Contains("1.10.0", parsed.Data.ServerSupportedVersions);
        }

        [Fact]
        public async Task PackageManifest_RoundTrip_SearchAndGet()
        {
            var manifest = CreateTestManifest("Api.RoundTrip");
            var post = await this.client.PostAsync(
                "/api/packageManifests",
                JsonContent(manifest));
            Assert.Equal(HttpStatusCode.OK, post.StatusCode);

            var searchBody = new ManifestSearchRequest
            {
                Query = new Utils.Models.Objects.SearchRequestMatch
                {
                    KeyWord = "RoundTrip",
                    MatchType = "Substring",
                },
            };
            var search = await this.client.PostAsync("/api/manifestSearch", JsonContent(searchBody));
            Assert.Equal(HttpStatusCode.OK, search.StatusCode);
            var searchJson = JObject.Parse(await search.Content.ReadAsStringAsync());
            var identifiers = searchJson["Data"]!
                .Select(token => token["PackageIdentifier"]!.ToString())
                .ToList();
            Assert.Contains("Api.RoundTrip", identifiers);

            var get = await this.client.GetAsync("/api/packageManifests/Api.RoundTrip");
            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            var getJson = JObject.Parse(await get.Content.ReadAsStringAsync());
            Assert.Equal("Api.RoundTrip", getJson["Data"]!["PackageIdentifier"]!.ToString());
            var installer = getJson["Data"]!["Versions"]![0]!["Installers"]![0]!;
            Assert.False(string.IsNullOrWhiteSpace(installer["InstallerUrl"]?.ToString()));
            Assert.False(string.IsNullOrWhiteSpace(installer["InstallerSha256"]?.ToString()));
        }

        [Fact]
        public async Task Write_WithoutApiKey_IsUnauthorized_WhenConfigured()
        {
            using var keyedFactory = new ServerApiFactoryWithKey();
            using var keyedClient = keyedFactory.CreateClient();

            var manifest = CreateTestManifest("Api.Protected");
            var unauthorized = await keyedClient.PostAsync("/api/packageManifests", JsonContent(manifest));
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/packageManifests")
            {
                Content = JsonContent(manifest),
            };
            request.Headers.Add("X-Api-Key", "test-secret-key");
            var authorized = await keyedClient.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
        }

        private static StringContent JsonContent(object value)
        {
            return new StringContent(
                JsonConvert.SerializeObject(value, JsonSettings),
                Encoding.UTF8,
                "application/json");
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
                            PackageName = "API Test App",
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
    }

    /// <summary>
    /// Test host that uses an isolated SQLite file.
    /// </summary>
    public class ServerApiFactory : WebApplicationFactory<Program>
    {
        private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"winget_api_{Guid.NewGuid():N}.db");
        private readonly string apiKey;

        public ServerApiFactory()
            : this(string.Empty)
        {
        }

        protected ServerApiFactory(string apiKey)
        {
            this.apiKey = apiKey;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Database:Path", this.dbPath);
            builder.UseSetting("ServerIdentifier", "test-restsource");
            builder.UseSetting("ApiSettings:ApiKey", this.apiKey);
            builder.UseEnvironment("Development");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { File.Delete(this.dbPath); } catch { }
            try { File.Delete(this.dbPath + "-wal"); } catch { }
            try { File.Delete(this.dbPath + "-shm"); } catch { }
        }
    }

    /// <summary>
    /// Test host with an API key required for writes.
    /// </summary>
    public class ServerApiFactoryWithKey : ServerApiFactory
    {
        public ServerApiFactoryWithKey()
            : base("test-secret-key")
        {
        }
    }
}
