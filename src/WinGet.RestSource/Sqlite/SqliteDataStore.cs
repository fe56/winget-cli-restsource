// -----------------------------------------------------------------------
// <copyright file="SqliteDataStore.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. Licensed under the MIT License.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.WinGet.RestSource.Sqlite
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Microsoft.Extensions.Logging;
    using Microsoft.WinGet.RestSource.Utils.Common;
    using Microsoft.WinGet.RestSource.Utils.Constants;
    using Microsoft.WinGet.RestSource.Utils.Constants.Enumerations;
    using Microsoft.WinGet.RestSource.Utils.Exceptions;
    using Microsoft.WinGet.RestSource.Utils.Models.Errors;
    using Microsoft.WinGet.RestSource.Utils.Models.ExtendedSchemas;
    using Microsoft.WinGet.RestSource.Utils.Models.Objects;
    using Microsoft.WinGet.RestSource.Utils.Models.Schemas;
    using Microsoft.WinGet.RestSource.Utils.Validators;
    using Newtonsoft.Json;
    using Version = Microsoft.WinGet.RestSource.Utils.Models.Schemas.Version;

    /// <summary>
    /// SQLite Data Store implementation of <see cref="IApiDataStore"/>.
    /// Stores each PackageManifest as a JSON document in a single SQLite table.
    /// </summary>
    public class SqliteDataStore : IApiDataStore
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
        };

        private static readonly List<string> SupportedQueryFields = new List<string>()
        {
            PackageMatchFields.PackageIdentifier,
            PackageMatchFields.PackageName,
            PackageMatchFields.Publisher,
            PackageMatchFields.Moniker,
            PackageMatchFields.Command,
            PackageMatchFields.Tag,
            PackageMatchFields.PackageFamilyName,
            PackageMatchFields.ProductCode,
            PackageMatchFields.UpgradeCode,
        };

        private readonly string connectionString;
        private readonly ILogger<SqliteDataStore> log;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqliteDataStore"/> class.
        /// </summary>
        /// <param name="log">Logger.</param>
        /// <param name="databasePath">Path to the SQLite database file.</param>
        public SqliteDataStore(ILogger<SqliteDataStore> log, string databasePath)
        {
            this.log = log;
            this.connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
            }.ToString();

            this.InitializeDatabase();
        }

        /// <inheritdoc />
        public Task<int> Count()
        {
            using var connection = this.OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM PackageManifests";
            return Task.FromResult(Convert.ToInt32(cmd.ExecuteScalar()));
        }

        /// <inheritdoc />
        public Task AddPackage(Package package)
        {
            PackageManifest packageManifest = new PackageManifest(package);
            ApiDataValidator.Validate(packageManifest);

            using var connection = this.OpenConnection();
            this.InsertManifest(connection, packageManifest);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task DeletePackage(string packageIdentifier)
        {
            using var connection = this.OpenConnection();
            this.DeleteManifestRow(connection, packageIdentifier);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task UpdatePackage(string packageIdentifier, Package package)
        {
            using var connection = this.OpenConnection();
            PackageManifest manifest = this.GetManifestOrThrow(connection, packageIdentifier);
            manifest.Update(package);

            ApiDataValidator.Validate(manifest);
            this.UpsertManifest(connection, manifest);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<ApiDataPage<Package>> GetPackages(string packageIdentifier, string continuationToken = null)
        {
            int offset = DecodeContinuationOffset(continuationToken);
            int pageSize = FunctionSettingsConstants.MaxResultsPerPage;

            using var connection = this.OpenConnection();
            List<PackageManifest> manifests;

            if (!string.IsNullOrWhiteSpace(packageIdentifier))
            {
                manifests = new List<PackageManifest>();
                PackageManifest m = this.GetManifest(connection, packageIdentifier);
                if (m != null)
                {
                    manifests.Add(m);
                }
            }
            else
            {
                manifests = this.GetAllManifests(connection, offset, pageSize + 1);
            }

            var result = new ApiDataPage<Package>();
            foreach (var m in manifests.Take(pageSize))
            {
                result.Items.Add(m.ToPackage());
            }

            result.ContinuationToken = manifests.Count > pageSize
                ? EncodeContinuationOffset(offset + pageSize)
                : null;

            return Task.FromResult(result);
        }

        /// <inheritdoc />
        public Task AddVersion(string packageIdentifier, Version version)
        {
            using var connection = this.OpenConnection();
            PackageManifest manifest = this.GetManifestOrThrow(connection, packageIdentifier);
            manifest.AddVersion(version);

            ApiDataValidator.Validate(manifest);
            this.UpsertManifest(connection, manifest);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task DeleteVersion(string packageIdentifier, string packageVersion)
        {
            using var connection = this.OpenConnection();
            PackageManifest manifest = this.GetManifestOrThrow(connection, packageIdentifier);
            manifest.RemoveVersion(packageVersion);

            if (manifest.Versions is null)
            {
                this.DeleteManifestRow(connection, packageIdentifier);
            }
            else
            {
                ApiDataValidator.Validate(manifest);
                this.UpsertManifest(connection, manifest);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task UpdateVersion(string packageIdentifier, string packageVersion, Version version)
        {
            using var connection = this.OpenConnection();
            PackageManifest manifest = this.GetManifestOrThrow(connection, packageIdentifier);
            manifest.Versions.Update(version);

            ApiDataValidator.Validate(manifest);
            this.UpsertManifest(connection, manifest);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<ApiDataPage<Version>> GetVersions(string packageIdentifier, string packageVersion)
        {
            using var connection = this.OpenConnection();
            PackageManifest manifest = this.GetManifestOrThrow(connection, packageIdentifier);

            var result = new ApiDataPage<Version>();
            result.Items = manifest.GetVersion(packageVersion).Select(ver => new Version(ver)).ToList();
            result.ContinuationToken = null;
            return Task.FromResult(result);
        }

        /// <inheritdoc />
        public Task AddInstaller(string packageIdentifier, string packageVersion, Installer installer)
        {
            using var connection = this.OpenConnection();
            PackageManifest manifest = this.GetManifestOrThrow(connection, packageIdentifier);
            manifest.AddInstaller(installer, packageVersion);

            ApiDataValidator.Validate(manifest);
            this.UpsertManifest(connection, manifest);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task DeleteInstaller(string packageIdentifier, string packageVersion, string installerIdentifier)
        {
            using var connection = this.OpenConnection();
            PackageManifest manifest = this.GetManifestOrThrow(connection, packageIdentifier);
            manifest.RemoveInstaller(installerIdentifier, packageVersion);

            if (manifest.GetVersion(packageVersion)[0].Installers is null)
            {
                // No installer left, delete the version
                manifest.RemoveVersion(packageVersion);
                if (manifest.Versions is null)
                {
                    this.DeleteManifestRow(connection, packageIdentifier);
                }
                else
                {
                    ApiDataValidator.Validate(manifest);
                    this.UpsertManifest(connection, manifest);
                }
            }
            else
            {
                ApiDataValidator.Validate(manifest);
                this.UpsertManifest(connection, manifest);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task UpdateInstaller(string packageIdentifier, string packageVersion, string installerIdentifier, Installer installer)
        {
            using var connection = this.OpenConnection();
            PackageManifest manifest = this.GetManifestOrThrow(connection, packageIdentifier);
            manifest.UpdateInstaller(installer, packageVersion);

            ApiDataValidator.Validate(manifest);
            this.UpsertManifest(connection, manifest);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<ApiDataPage<Installer>> GetInstallers(string packageIdentifier, string packageVersion, string installerIdentifier)
        {
            using var connection = this.OpenConnection();
            PackageManifest manifest = this.GetManifestOrThrow(connection, packageIdentifier);

            var result = new ApiDataPage<Installer>();
            result.Items = manifest.GetInstaller(installerIdentifier, packageVersion).Select(i => new Installer(i)).ToList();
            result.ContinuationToken = null;
            return Task.FromResult(result);
        }

        /// <inheritdoc />
        public Task AddLocale(string packageIdentifier, string packageVersion, Locale locale)
        {
            using var connection = this.OpenConnection();
            PackageManifest manifest = this.GetManifestOrThrow(connection, packageIdentifier);
            manifest.AddLocale(locale, packageVersion);

            ApiDataValidator.Validate(manifest);
            this.UpsertManifest(connection, manifest);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task DeleteLocale(string packageIdentifier, string packageVersion, string packageLocale)
        {
            using var connection = this.OpenConnection();
            PackageManifest manifest = this.GetManifestOrThrow(connection, packageIdentifier);
            manifest.RemoveLocale(packageLocale, packageVersion);

            ApiDataValidator.Validate(manifest);
            this.UpsertManifest(connection, manifest);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task UpdateLocale(string packageIdentifier, string packageVersion, string packageLocale, Locale locale)
        {
            using var connection = this.OpenConnection();
            PackageManifest manifest = this.GetManifestOrThrow(connection, packageIdentifier);
            manifest.UpdateLocale(locale, packageVersion);

            ApiDataValidator.Validate(manifest);
            this.UpsertManifest(connection, manifest);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<ApiDataPage<Locale>> GetLocales(string packageIdentifier, string packageVersion, string packageLocale)
        {
            using var connection = this.OpenConnection();
            PackageManifest manifest = this.GetManifestOrThrow(connection, packageIdentifier);

            var result = new ApiDataPage<Locale>();
            result.Items = manifest.GetLocale(packageLocale, packageVersion).Select(l => new Locale(l)).ToList();
            result.ContinuationToken = null;
            return Task.FromResult(result);
        }

        /// <inheritdoc />
        public Task AddPackageManifest(PackageManifest packageManifest)
        {
            using var connection = this.OpenConnection();
            this.InsertManifest(connection, packageManifest);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task DeletePackageManifest(string packageIdentifier)
        {
            return this.DeletePackage(packageIdentifier);
        }

        /// <inheritdoc />
        public Task UpdatePackageManifest(string packageIdentifier, PackageManifest packageManifest)
        {
            using var connection = this.OpenConnection();

            // Ensure the manifest exists before updating
            this.GetManifestOrThrow(connection, packageIdentifier);
            this.UpsertManifest(connection, packageManifest);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<ApiDataPage<PackageManifest>> GetPackageManifests(string packageIdentifier, string continuationToken = null, string versionFilter = null, string channelFilter = null, string marketFilter = null)
        {
            int offset = DecodeContinuationOffset(continuationToken);
            int pageSize = FunctionSettingsConstants.MaxResultsPerPage;

            using var connection = this.OpenConnection();
            List<PackageManifest> manifests;

            if (!string.IsNullOrWhiteSpace(packageIdentifier))
            {
                manifests = new List<PackageManifest>();
                PackageManifest m = this.GetManifest(connection, packageIdentifier);
                if (m != null)
                {
                    manifests.Add(m);
                }
            }
            else
            {
                manifests = this.GetAllManifests(connection, offset, pageSize + 1);
            }

            var result = new ApiDataPage<PackageManifest>();
            foreach (var m in manifests.Take(pageSize))
            {
                result.Items.Add(m);
            }

            result.ContinuationToken = manifests.Count > pageSize
                ? EncodeContinuationOffset(offset + pageSize)
                : null;

            // Apply Version Filter
            if (!string.IsNullOrEmpty(versionFilter))
            {
                foreach (PackageManifest manifest in result.Items)
                {
                    if (manifest.Versions != null)
                    {
                        manifest.Versions = new VersionsExtended(manifest.Versions.Where(v => v.PackageVersion.Equals(versionFilter)));
                    }
                }
            }

            // Apply Channel Filter
            if (!string.IsNullOrEmpty(channelFilter))
            {
                foreach (PackageManifest manifest in result.Items)
                {
                    if (manifest.Versions != null)
                    {
                        manifest.Versions = new VersionsExtended(manifest.Versions.Where(v => v.Channel != null && v.Channel.Equals(channelFilter)));
                    }
                }
            }

            // Apply Market Filter
            if (!string.IsNullOrEmpty(marketFilter))
            {
                ApplyMarketFilter(result.Items, marketFilter);
            }

            return Task.FromResult(result);
        }

        /// <inheritdoc />
        public Task<ApiDataPage<ManifestSearchResponse>> SearchPackageManifests(ManifestSearchRequest manifestSearchRequest, string continuationToken = null)
        {
            using var connection = this.OpenConnection();
            List<PackageManifest> allManifests = this.GetAllManifests(connection);

            manifestSearchRequest ??= new ManifestSearchRequest();
            manifestSearchRequest.Inclusions ??= new Utils.Models.Arrays.SearchRequestPackageMatchFilter();
            manifestSearchRequest.Filters ??= new Utils.Models.Arrays.SearchRequestPackageMatchFilter();

            // Convert Query to inclusions (same as CosmosDataStore)
            if (manifestSearchRequest.Query != null)
            {
                manifestSearchRequest.Inclusions.AddRange(SupportedQueryFields.Select(q => new SearchRequestPackageMatchFilter()
                {
                    PackageMatchField = q,
                    RequestMatch = manifestSearchRequest.Query,
                }));
            }

            // Filter manifests
            var matchedManifests = allManifests.Where(m => MatchesSearch(m, manifestSearchRequest)).ToList();

            // Convert to search responses
            List<ManifestSearchResponse> searchResponses = matchedManifests
                .Distinct()
                .SelectMany(m => ManifestSearchResponse.GetSearchVersions(m))
                .ToList();

            // Consolidate
            searchResponses = ManifestSearchResponse.Consolidate(searchResponses)
                .OrderBy(r => r.PackageIdentifier)
                .ToList();

            // Apply pagination
            int offset = DecodeContinuationOffset(continuationToken);
            int pageSize = FunctionSettingsConstants.MaxResultsPerPage;

            var result = new ApiDataPage<ManifestSearchResponse>();
            result.Items = searchResponses.Skip(offset).Take(pageSize).ToList();
            result.ContinuationToken = (offset + pageSize) < searchResponses.Count
                ? EncodeContinuationOffset(offset + pageSize)
                : null;

            return Task.FromResult(result);
        }

        private static bool MatchesSearch(PackageManifest manifest, ManifestSearchRequest request)
        {
            bool inclusionMatch = false;
            bool filterMatch = true;

            // Process inclusions (OR logic): at least one must match if any are specified
            if (request.Inclusions != null && request.Inclusions.Count > 0)
            {
                foreach (var inclusion in request.Inclusions)
                {
                    if (IsPackageMatchFieldSupported(inclusion.PackageMatchField) &&
                        IsMatchTypeSupported(inclusion.RequestMatch.MatchType) &&
                        MatchesField(manifest, inclusion))
                    {
                        inclusionMatch = true;
                        break;
                    }
                }
            }
            else
            {
                // No inclusions means everything is included
                inclusionMatch = true;
            }

            // Process filters (AND logic): all must match
            if (request.Filters != null && request.Filters.Count > 0)
            {
                foreach (var filter in request.Filters)
                {
                    if (IsPackageMatchFieldSupported(filter.PackageMatchField) &&
                        IsMatchTypeSupported(filter.RequestMatch.MatchType))
                    {
                        if (!MatchesField(manifest, filter))
                        {
                            filterMatch = false;
                            break;
                        }
                    }
                }
            }

            return inclusionMatch && filterMatch;
        }

        private static bool MatchesField(PackageManifest manifest, SearchRequestPackageMatchFilter condition)
        {
            string keyword = condition.RequestMatch.KeyWord;
            string matchType = condition.RequestMatch.MatchType;

            switch (condition.PackageMatchField)
            {
                case PackageMatchFields.PackageIdentifier:
                    return MatchString(manifest.PackageIdentifier, keyword, matchType);

                case PackageMatchFields.PackageName:
                    return manifest.Versions != null && manifest.Versions.Any(
                        v => v.DefaultLocale != null && MatchString(v.DefaultLocale.PackageName, keyword, matchType));

                case PackageMatchFields.Publisher:
                    return manifest.Versions != null && manifest.Versions.Any(
                        v => v.DefaultLocale != null && MatchString(v.DefaultLocale.Publisher, keyword, matchType));

                case PackageMatchFields.Moniker:
                    return manifest.Versions != null && manifest.Versions.Any(
                        v => v.DefaultLocale != null && MatchString(v.DefaultLocale.Moniker, keyword, matchType));

                case PackageMatchFields.Tag:
                    return manifest.Versions != null && manifest.Versions.Any(
                        v => v.DefaultLocale?.Tags != null && v.DefaultLocale.Tags.Any(t => MatchString(t, keyword, matchType)));

                case PackageMatchFields.Command:
                    return manifest.Versions != null && manifest.Versions.Any(
                        v => v.Installers != null && v.Installers.Any(
                            i => i.Commands != null && i.Commands.Any(c => MatchString(c, keyword, matchType))));

                case PackageMatchFields.PackageFamilyName:
                    return manifest.Versions != null && manifest.Versions.Any(
                        v => v.Installers != null && v.Installers.Any(
                            i => MatchString(i.PackageFamilyName, keyword, matchType)));

                case PackageMatchFields.ProductCode:
                    return manifest.Versions != null && manifest.Versions.Any(
                        v => v.Installers != null && v.Installers.Any(
                            i => MatchString(i.ProductCode, keyword, matchType) ||
                                 (i.AppsAndFeaturesEntries != null && i.AppsAndFeaturesEntries.Any(
                                     e => MatchString(e.ProductCode, keyword, matchType)))));

                case PackageMatchFields.UpgradeCode:
                    return manifest.Versions != null && manifest.Versions.Any(
                        v => v.Installers != null && v.Installers.Any(
                            i => i.AppsAndFeaturesEntries != null && i.AppsAndFeaturesEntries.Any(
                                e => MatchString(e.UpgradeCode, keyword, matchType))));

                case PackageMatchFields.HasInstallerType:
                    return manifest.Versions != null && manifest.Versions.Any(
                        v => v.Installers != null && v.Installers.Any(
                            i => MatchString(i.InstallerType, keyword, matchType) ||
                                 MatchString(i.NestedInstallerType, keyword, matchType)));

                default:
                    return false;
            }
        }

        private static bool MatchString(string field, string keyword, string matchType)
        {
            if (field == null)
            {
                return false;
            }

            return matchType switch
            {
                MatchType.Exact => field.Equals(keyword, StringComparison.Ordinal),
                MatchType.CaseInsensitive => field.Equals(keyword, StringComparison.OrdinalIgnoreCase),
                MatchType.StartsWith => field.StartsWith(keyword, StringComparison.OrdinalIgnoreCase),
                MatchType.Substring => field.Contains(keyword, StringComparison.OrdinalIgnoreCase),
                _ => false,
            };
        }

        private static bool IsPackageMatchFieldSupported(string packageMatchField)
        {
            switch (packageMatchField)
            {
                case PackageMatchFields.PackageIdentifier:
                case PackageMatchFields.PackageName:
                case PackageMatchFields.Publisher:
                case PackageMatchFields.PackageFamilyName:
                case PackageMatchFields.ProductCode:
                case PackageMatchFields.UpgradeCode:
                case PackageMatchFields.Tag:
                case PackageMatchFields.Command:
                case PackageMatchFields.Moniker:
                case PackageMatchFields.HasInstallerType:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsMatchTypeSupported(string matchType)
        {
            switch (matchType)
            {
                case MatchType.Exact:
                case MatchType.CaseInsensitive:
                case MatchType.StartsWith:
                case MatchType.Substring:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Applies market filter to package manifests by removing installers that don't match the market filter.
        /// </summary>
        private static void ApplyMarketFilter(IList<PackageManifest> packageManifests, string marketFilter)
        {
            if (string.IsNullOrEmpty(marketFilter))
            {
                return;
            }

            foreach (PackageManifest packageManifest in packageManifests)
            {
                if (packageManifest.Versions != null)
                {
                    HashSet<string> versionsWithoutInstallers = new HashSet<string>();
                    foreach (VersionExtended version in packageManifest.Versions)
                    {
                        if (version.Installers != null)
                        {
                            HashSet<string> installersNotMatchingFilter = new HashSet<string>();
                            foreach (var installer in version.Installers)
                            {
                                if (installer.Markets == null
                                    || (installer.Markets.AllowedMarkets == null && installer.Markets.ExcludedMarkets == null)
                                    || (installer.Markets.AllowedMarkets != null && !installer.Markets.AllowedMarkets.Contains(marketFilter))
                                    || (installer.Markets.ExcludedMarkets != null && installer.Markets.ExcludedMarkets.Contains(marketFilter)))
                                {
                                    installersNotMatchingFilter.Add(installer.InstallerIdentifier);
                                }
                            }

                            foreach (var installer in installersNotMatchingFilter)
                            {
                                version.RemoveInstaller(installer);
                            }
                        }

                        if (version.Installers == null || version.Installers.Count() == 0)
                        {
                            versionsWithoutInstallers.Add(version.PackageVersion);
                        }
                    }

                    foreach (var version in versionsWithoutInstallers)
                    {
                        packageManifest.RemoveVersion(version);
                    }
                }
            }
        }

        private static int DecodeContinuationOffset(string continuationToken)
        {
            if (string.IsNullOrEmpty(continuationToken))
            {
                return 0;
            }

            string decoded = StringEncoder.DecodeContinuationToken(continuationToken);
            if (int.TryParse(decoded, out int offset))
            {
                return offset;
            }

            return 0;
        }

        private static string EncodeContinuationOffset(int offset)
        {
            return StringEncoder.EncodeContinuationToken(offset.ToString());
        }

        /// <summary>
        /// Initializes the database schema.
        /// </summary>
        private void InitializeDatabase()
        {
            using var connection = this.OpenConnection();

            using var pragmaCmd = connection.CreateCommand();
            pragmaCmd.CommandText = "PRAGMA journal_mode=WAL;";
            pragmaCmd.ExecuteNonQuery();

            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS PackageManifests (
                    PackageIdentifier TEXT PRIMARY KEY,
                    Data TEXT NOT NULL,
                    LastModified TEXT NOT NULL DEFAULT (datetime('now'))
                )";
            createCmd.ExecuteNonQuery();
        }

        private SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection(this.connectionString);
            connection.Open();
            return connection;
        }

        private PackageManifest GetManifest(SqliteConnection connection, string packageIdentifier)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Data FROM PackageManifests WHERE PackageIdentifier = @id";
            cmd.Parameters.AddWithValue("@id", packageIdentifier);

            var data = cmd.ExecuteScalar() as string;
            if (data == null)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<PackageManifest>(data, JsonSettings);
        }

        private PackageManifest GetManifestOrThrow(SqliteConnection connection, string packageIdentifier)
        {
            var manifest = this.GetManifest(connection, packageIdentifier);
            if (manifest == null)
            {
                throw new DefaultException(
                    new InternalRestError(
                        ErrorConstants.ResourceNotFoundErrorCode,
                        ErrorConstants.ResourceNotFoundErrorMessage));
            }

            return manifest;
        }

        private List<PackageManifest> GetAllManifests(SqliteConnection connection, int offset = 0, int limit = -1)
        {
            using var cmd = connection.CreateCommand();
            if (limit > 0)
            {
                cmd.CommandText = "SELECT Data FROM PackageManifests ORDER BY PackageIdentifier LIMIT @limit OFFSET @offset";
                cmd.Parameters.AddWithValue("@limit", limit);
                cmd.Parameters.AddWithValue("@offset", offset);
            }
            else
            {
                cmd.CommandText = "SELECT Data FROM PackageManifests ORDER BY PackageIdentifier";
            }

            var manifests = new List<PackageManifest>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string json = reader.GetString(0);
                manifests.Add(JsonConvert.DeserializeObject<PackageManifest>(json, JsonSettings));
            }

            return manifests;
        }

        private void InsertManifest(SqliteConnection connection, PackageManifest manifest)
        {
            string json = JsonConvert.SerializeObject(manifest, JsonSettings);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO PackageManifests (PackageIdentifier, Data, LastModified)
                VALUES (@id, @data, datetime('now'))";
            cmd.Parameters.AddWithValue("@id", manifest.PackageIdentifier);
            cmd.Parameters.AddWithValue("@data", json);

            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                // SQLITE_CONSTRAINT - unique constraint violation means the resource already exists
                throw new DefaultException(
                    new InternalRestError(
                        ErrorConstants.ResourceConflictErrorCode,
                        ErrorConstants.ResourceConflictErrorMessage));
            }
        }

        private void UpsertManifest(SqliteConnection connection, PackageManifest manifest)
        {
            string json = JsonConvert.SerializeObject(manifest, JsonSettings);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO PackageManifests (PackageIdentifier, Data, LastModified)
                VALUES (@id, @data, datetime('now'))";
            cmd.Parameters.AddWithValue("@id", manifest.PackageIdentifier);
            cmd.Parameters.AddWithValue("@data", json);
            cmd.ExecuteNonQuery();
        }

        private void DeleteManifestRow(SqliteConnection connection, string packageIdentifier)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM PackageManifests WHERE PackageIdentifier = @id";
            cmd.Parameters.AddWithValue("@id", packageIdentifier);

            int rows = cmd.ExecuteNonQuery();
            if (rows == 0)
            {
                throw new DefaultException(
                    new InternalRestError(
                        ErrorConstants.ResourceNotFoundErrorCode,
                        ErrorConstants.ResourceNotFoundErrorMessage));
            }
        }
    }
}
