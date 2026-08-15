namespace Microsoft.WinGet.RestSource.Server.Services
{
    using Microsoft.WinGet.RestSource.Utils;
    using Microsoft.WinGet.RestSource.Utils.Common;
    using Microsoft.WinGet.RestSource.Utils.Constants.Enumerations;
    using Microsoft.WinGet.RestSource.Utils.Exceptions;
    using Microsoft.WinGet.RestSource.Utils.Models.Objects;
    using Microsoft.WinGet.RestSource.Utils.Models.Schemas;
    using Microsoft.WinGetUtil.Models.V1;
    using Newtonsoft.Json;

    /// <summary>
    /// Shared catalog operations for the admin UI and tests.
    /// </summary>
    public class PackageCatalogService
    {
        private const int MaxListPages = 500;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
        };

        private readonly IApiDataStore dataStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="PackageCatalogService"/> class.
        /// </summary>
        /// <param name="dataStore">Package data store.</param>
        public PackageCatalogService(IApiDataStore dataStore)
        {
            this.dataStore = dataStore;
        }

        /// <summary>
        /// Formats a store or validation exception for display.
        /// </summary>
        /// <param name="exception">Exception to format.</param>
        /// <returns>User-facing message.</returns>
        public static string GetErrorMessage(Exception exception)
        {
            if (exception is DefaultException defaultException &&
                !string.IsNullOrWhiteSpace(defaultException.InternalRestError?.ErrorMessage))
            {
                return defaultException.InternalRestError.ErrorMessage;
            }

            return exception.Message;
        }

        /// <summary>
        /// Returns the number of packages in the store.
        /// </summary>
        /// <returns>Package count.</returns>
        public Task<int> CountAsync()
        {
            return this.dataStore.Count();
        }

        /// <summary>
        /// Loads every package manifest, following continuation tokens.
        /// </summary>
        /// <returns>All manifests.</returns>
        public async Task<IReadOnlyList<PackageManifest>> GetAllManifestsAsync()
        {
            var manifests = new List<PackageManifest>();
            string? continuationToken = null;

            for (int page = 0; page < MaxListPages; page++)
            {
                var result = await this.dataStore.GetPackageManifests(null, continuationToken);
                if (result.Items != null)
                {
                    manifests.AddRange(result.Items);
                }

                if (string.IsNullOrEmpty(result.ContinuationToken))
                {
                    break;
                }

                continuationToken = result.ContinuationToken;
            }

            return manifests;
        }

        /// <summary>
        /// Searches manifests, or lists all when the query is empty.
        /// </summary>
        /// <param name="query">Optional substring query.</param>
        /// <returns>Matching manifests.</returns>
        public async Task<IReadOnlyList<PackageManifest>> SearchAsync(string? query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return await this.GetAllManifestsAsync();
            }

            var searchRequest = new ManifestSearchRequest
            {
                Query = new SearchRequestMatch
                {
                    KeyWord = query,
                    MatchType = MatchType.Substring,
                },
            };

            var searchResult = await this.dataStore.SearchPackageManifests(searchRequest);
            var manifests = new List<PackageManifest>();
            var identifiers = searchResult.Items?
                .Select(item => item.PackageIdentifier)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            foreach (var identifier in identifiers)
            {
                var page = await this.dataStore.GetPackageManifests(identifier);
                if (page.Items != null)
                {
                    manifests.AddRange(page.Items);
                }
            }

            return manifests;
        }

        /// <summary>
        /// Gets a single package manifest.
        /// </summary>
        /// <param name="packageIdentifier">Package identifier.</param>
        /// <returns>The manifest, or null if it does not exist.</returns>
        public async Task<PackageManifest?> GetAsync(string packageIdentifier)
        {
            var result = await this.dataStore.GetPackageManifests(packageIdentifier);
            return result.Items?.FirstOrDefault();
        }

        /// <summary>
        /// Adds a package from REST PackageManifest JSON.
        /// </summary>
        /// <param name="json">JSON document.</param>
        /// <returns>The stored manifest.</returns>
        public async Task<PackageManifest> AddFromJsonAsync(string json)
        {
            var parsed = DeserializeManifest(json);
            await this.dataStore.AddPackageManifest(parsed);
            return parsed;
        }

        /// <summary>
        /// Replaces a package from REST PackageManifest JSON.
        /// </summary>
        /// <param name="packageIdentifier">Existing identifier.</param>
        /// <param name="json">JSON document.</param>
        /// <returns>The stored manifest.</returns>
        public async Task<PackageManifest> UpdateFromJsonAsync(string packageIdentifier, string json)
        {
            var parsed = DeserializeManifest(json);
            if (!string.Equals(parsed.PackageIdentifier, packageIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Package identifier in JSON ({parsed.PackageIdentifier}) does not match {packageIdentifier}.");
            }

            await this.dataStore.UpdatePackageManifest(packageIdentifier, parsed);
            return parsed;
        }

        /// <summary>
        /// Adds or merges a winget YAML manifest.
        /// </summary>
        /// <param name="yaml">Merged YAML text.</param>
        /// <returns>The stored manifest.</returns>
        public async Task<PackageManifest> AddFromYamlAsync(string yaml)
        {
            if (string.IsNullOrWhiteSpace(yaml))
            {
                throw new InvalidOperationException("YAML content is empty.");
            }

            Manifest manifest = Manifest.CreateManifestFromString(yaml);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Id))
            {
                throw new InvalidOperationException("Failed to parse YAML as a winget package manifest.");
            }

            PackageManifest? existing = await this.GetAsync(manifest.Id);
            PackageManifest packageManifest = PackageManifestUtils.AddManifestToPackageManifest(manifest, existing);

            if (existing == null)
            {
                await this.dataStore.AddPackageManifest(packageManifest);
            }
            else
            {
                await this.dataStore.UpdatePackageManifest(manifest.Id, packageManifest);
            }

            return packageManifest;
        }

        /// <summary>
        /// Deletes a package manifest.
        /// </summary>
        /// <param name="packageIdentifier">Package identifier.</param>
        /// <returns>A task.</returns>
        public Task DeleteAsync(string packageIdentifier)
        {
            return this.dataStore.DeletePackageManifest(packageIdentifier);
        }

        /// <summary>
        /// Serializes a manifest for the JSON editor.
        /// </summary>
        /// <param name="manifest">Manifest.</param>
        /// <returns>Indented JSON.</returns>
        public static string ToPrettyJson(PackageManifest manifest)
        {
            return JsonConvert.SerializeObject(manifest, Formatting.Indented, JsonSettings);
        }

        private static PackageManifest DeserializeManifest(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("JSON content is empty.");
            }

            var parsed = JsonConvert.DeserializeObject<PackageManifest>(json, JsonSettings);
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.PackageIdentifier))
            {
                throw new InvalidOperationException("Failed to parse JSON as a PackageManifest.");
            }

            return parsed;
        }
    }
}
