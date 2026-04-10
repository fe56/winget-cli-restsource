// -----------------------------------------------------------------------
// <copyright file="VersionController.cs" company="Microsoft Corporation">
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
    using Microsoft.WinGet.RestSource.Utils.Models.Errors;
    using Microsoft.WinGet.RestSource.Utils.Validators;
    using Version = Microsoft.WinGet.RestSource.Utils.Models.Schemas.Version;

    /// <summary>
    /// Controller for version operations.
    /// </summary>
    [ApiController]
    [Route("api")]
    public class VersionController : ControllerBase
    {
        private readonly IApiDataStore dataStore;
        private readonly ILogger<VersionController> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="VersionController"/> class.
        /// </summary>
        /// <param name="dataStore">Data Store.</param>
        /// <param name="logger">Logger.</param>
        public VersionController(IApiDataStore dataStore, ILogger<VersionController> logger)
        {
            this.dataStore = dataStore;
            this.logger = logger;
        }

        /// <summary>
        /// Add a version.
        /// </summary>
        /// <param name="packageIdentifier">Package identifier.</param>
        /// <returns>IActionResult.</returns>
        [HttpPost("packages/{packageIdentifier}/versions")]
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
        public async Task<IActionResult> VersionsPost(string packageIdentifier)
        {
            Version version = null;

            try
            {
                // Parse Headers
                HeaderProcessor.ToDictionary(this.Request.Headers);

                // Parse body as Version
                version = await Parser.StreamParser<Version>(this.Request.Body, this.logger);
                ApiDataValidator.Validate(version);

                // Save Document
                await this.dataStore.AddVersion(packageIdentifier, version);
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

            return new ApiObjectResult(new ApiResponse<Version>(version));
        }

        /// <summary>
        /// Get versions.
        /// </summary>
        /// <param name="packageIdentifier">Package identifier.</param>
        /// <param name="packageVersion">Optional version.</param>
        /// <returns>IActionResult.</returns>
        [HttpGet("packages/{packageIdentifier}/versions/{packageVersion?}")]
        public async Task<IActionResult> VersionsGet(string packageIdentifier, string packageVersion = null)
        {
            ApiDataPage<Version> versions;

            try
            {
                // Parse Headers
                HeaderProcessor.ToDictionary(this.Request.Headers);

                versions = await this.dataStore.GetVersions(packageIdentifier, packageVersion);
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

            return versions.Items.Count switch
            {
                0 => new NoContentResult(),
                1 => new ApiObjectResult(new ApiResponse<Version>(versions.Items.First(), versions.ContinuationToken)),
                _ => new ApiObjectResult(new ApiResponse<List<Version>>(versions.Items.ToList(), versions.ContinuationToken)),
            };
        }

        /// <summary>
        /// Update a version.
        /// </summary>
        /// <param name="packageIdentifier">Package identifier.</param>
        /// <param name="packageVersion">Version.</param>
        /// <returns>IActionResult.</returns>
        [HttpPut("packages/{packageIdentifier}/versions/{packageVersion}")]
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
        public async Task<IActionResult> VersionsPut(string packageIdentifier, string packageVersion)
        {
            Version version = null;

            try
            {
                // Parse Headers
                HeaderProcessor.ToDictionary(this.Request.Headers);

                // Parse body as Version
                version = await Parser.StreamParser<Version>(this.Request.Body, this.logger);
                ApiDataValidator.Validate(version);

                // Validate Versions Match
                if (version.PackageVersion != packageVersion)
                {
                    throw new InvalidArgumentException(
                        new InternalRestError(
                            ErrorConstants.VersionDoesNotMatchErrorCode,
                            ErrorConstants.VersionDoesNotMatchErrorMessage));
                }

                await this.dataStore.UpdateVersion(packageIdentifier, packageVersion, version);
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

            return new ApiObjectResult(new ApiResponse<Version>(version));
        }

        /// <summary>
        /// Delete a version.
        /// </summary>
        /// <param name="packageIdentifier">Package identifier.</param>
        /// <param name="packageVersion">Version.</param>
        /// <returns>IActionResult.</returns>
        [HttpDelete("packages/{packageIdentifier}/versions/{packageVersion}")]
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
        public async Task<IActionResult> VersionsDelete(string packageIdentifier, string packageVersion)
        {
            try
            {
                // Parse Headers
                HeaderProcessor.ToDictionary(this.Request.Headers);
                await this.dataStore.DeleteVersion(packageIdentifier, packageVersion);
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
