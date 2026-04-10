// -----------------------------------------------------------------------
// <copyright file="PackageManifestController.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. Licensed under the MIT License.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.WinGet.RestSource.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Microsoft.WinGet.RestSource.Server.Common;
    using Microsoft.WinGet.RestSource.Server.Middleware;
    using Microsoft.WinGet.RestSource.Utils.Common;
    using Microsoft.WinGet.RestSource.Utils.Constants;
    using Microsoft.WinGet.RestSource.Utils.Exceptions;
    using Microsoft.WinGet.RestSource.Utils.Models;
    using Microsoft.WinGet.RestSource.Utils.Models.Arrays;
    using Microsoft.WinGet.RestSource.Utils.Models.Errors;
    using Microsoft.WinGet.RestSource.Utils.Models.Schemas;
    using Microsoft.WinGet.RestSource.Utils.Validators;

    /// <summary>
    /// Controller for package manifest operations.
    /// </summary>
    [ApiController]
    [Route("api")]
    public class PackageManifestController : ControllerBase
    {
        private readonly IApiDataStore dataStore;
        private readonly ILogger<PackageManifestController> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PackageManifestController"/> class.
        /// </summary>
        /// <param name="dataStore">Data Store.</param>
        /// <param name="logger">Logger.</param>
        public PackageManifestController(IApiDataStore dataStore, ILogger<PackageManifestController> logger)
        {
            this.dataStore = dataStore;
            this.logger = logger;
        }

        /// <summary>
        /// Add a package manifest.
        /// </summary>
        /// <returns>IActionResult.</returns>
        [HttpPost("packageManifests")]
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
        public async Task<IActionResult> ManifestPost()
        {
            PackageManifest packageManifest = null;

            try
            {
                // Parse Headers
                HeaderProcessor.ToDictionary(this.Request.Headers);

                // Parse Stream
                packageManifest = await Parser.StreamParser<PackageManifest>(this.Request.Body, this.logger);
                ApiDataValidator.Validate(packageManifest);

                await this.dataStore.AddPackageManifest(packageManifest);
            }
            catch (DefaultException e)
            {
                this.logger.LogError(e.ToString());
                return ActionResultHelper.ProcessError(e.InternalRestError);
            }
            catch (Exception e)
            {
                this.logger.LogError(e.ToString());
                return ActionResultHelper.UnhandledError(e);
            }

            return new ApiObjectResult(new ApiResponse<PackageManifest>(packageManifest));
        }

        /// <summary>
        /// Get package manifests.
        /// </summary>
        /// <param name="packageIdentifier">Optional package identifier.</param>
        /// <returns>IActionResult.</returns>
        [HttpGet("packageManifests/{packageIdentifier?}")]
        public async Task<IActionResult> ManifestGet(string packageIdentifier = null)
        {
            ApiDataPage<PackageManifest> manifests;
            QueryParameters unsupportedQueryParameters;
            QueryParameters requiredQueryParameters;
            Dictionary<string, string> headers;

            try
            {
                // Parse Headers
                headers = HeaderProcessor.ToDictionary(this.Request.Headers);
                string continuationToken = headers.GetValueOrDefault(QueryConstants.ContinuationToken);

                string versionFilter = null;
                string channelFilter = null;
                string marketFilter = null;

                // Schema supports query parameters only when PackageIdentifier is specified.
                if (!string.IsNullOrWhiteSpace(packageIdentifier))
                {
                    versionFilter = this.Request.Query[QueryConstants.Version];
                    channelFilter = this.Request.Query[QueryConstants.Channel];
                    marketFilter = this.Request.Query[QueryConstants.Market];
                }

                manifests = await this.dataStore.GetPackageManifests(packageIdentifier, continuationToken, versionFilter, channelFilter, marketFilter);
                unsupportedQueryParameters = UnsupportedAndRequiredFieldsHelper.GetUnsupportedQueryParametersFromRequest(this.Request.Query, ApiConstants.UnsupportedQueryParameters);
                requiredQueryParameters = UnsupportedAndRequiredFieldsHelper.GetRequiredQueryParametersFromRequest(this.Request.Query, ApiConstants.RequiredQueryParameters);
            }
            catch (DefaultException e)
            {
                this.logger.LogError(e.ToString());
                return ActionResultHelper.ProcessError(e.InternalRestError);
            }
            catch (Exception e)
            {
                this.logger.LogError(e.ToString());
                return ActionResultHelper.UnhandledError(e);
            }

            return manifests.Items.Count switch
            {
                0 => new NoContentResult(),
                1 => new ApiObjectResult(new GetPackageManifestApiResponse<PackageManifest>(manifests.Items.First(), manifests.ContinuationToken)
                {
                    UnsupportedQueryParameters = unsupportedQueryParameters,
                    RequiredQueryParameters = requiredQueryParameters,
                }),
                _ => new ApiObjectResult(new GetPackageManifestApiResponse<List<PackageManifest>>(manifests.Items.ToList(), manifests.ContinuationToken)
                {
                    UnsupportedQueryParameters = unsupportedQueryParameters,
                    RequiredQueryParameters = requiredQueryParameters,
                }),
            };
        }

        /// <summary>
        /// Update a package manifest.
        /// </summary>
        /// <param name="packageIdentifier">Package identifier.</param>
        /// <returns>IActionResult.</returns>
        [HttpPut("packageManifests/{packageIdentifier}")]
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
        public async Task<IActionResult> ManifestPut(string packageIdentifier)
        {
            PackageManifest packageManifest = null;

            try
            {
                // Parse Headers
                HeaderProcessor.ToDictionary(this.Request.Headers);

                // Parse Stream
                packageManifest = await Parser.StreamParser<PackageManifest>(this.Request.Body, this.logger);
                ApiDataValidator.Validate(packageManifest);

                // Validate Versions Match
                if (packageManifest.PackageIdentifier != packageIdentifier)
                {
                    throw new InvalidArgumentException(
                        new InternalRestError(
                            ErrorConstants.PackageDoesNotMatchErrorCode,
                            ErrorConstants.PackageDoesNotMatchErrorMessage));
                }

                await this.dataStore.UpdatePackageManifest(packageIdentifier, packageManifest);
            }
            catch (DefaultException e)
            {
                this.logger.LogError(e.ToString());
                return ActionResultHelper.ProcessError(e.InternalRestError);
            }
            catch (Exception e)
            {
                this.logger.LogError(e.ToString());
                return ActionResultHelper.UnhandledError(e);
            }

            return new ApiObjectResult(new ApiResponse<PackageManifest>(packageManifest));
        }

        /// <summary>
        /// Delete a package manifest.
        /// </summary>
        /// <param name="packageIdentifier">Package identifier.</param>
        /// <returns>IActionResult.</returns>
        [HttpDelete("packageManifests/{packageIdentifier}")]
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
        public async Task<IActionResult> ManifestDelete(string packageIdentifier)
        {
            try
            {
                // Parse Headers
                HeaderProcessor.ToDictionary(this.Request.Headers);
                await this.dataStore.DeletePackageManifest(packageIdentifier);
            }
            catch (DefaultException e)
            {
                this.logger.LogError(e.ToString());
                return ActionResultHelper.ProcessError(e.InternalRestError);
            }
            catch (Exception e)
            {
                this.logger.LogError(e.ToString());
                return ActionResultHelper.UnhandledError(e);
            }

            return new NoContentResult();
        }
    }
}
