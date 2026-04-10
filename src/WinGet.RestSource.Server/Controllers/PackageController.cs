// -----------------------------------------------------------------------
// <copyright file="PackageController.cs" company="Microsoft Corporation">
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
    using Microsoft.WinGet.RestSource.Utils.Models.Schemas;
    using Microsoft.WinGet.RestSource.Utils.Validators;

    /// <summary>
    /// Controller for package operations.
    /// </summary>
    [ApiController]
    [Route("api")]
    public class PackageController : ControllerBase
    {
        private readonly IApiDataStore dataStore;
        private readonly ILogger<PackageController> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PackageController"/> class.
        /// </summary>
        /// <param name="dataStore">Data Store.</param>
        /// <param name="logger">Logger.</param>
        public PackageController(IApiDataStore dataStore, ILogger<PackageController> logger)
        {
            this.dataStore = dataStore;
            this.logger = logger;
        }

        /// <summary>
        /// Add a package.
        /// </summary>
        /// <returns>IActionResult.</returns>
        [HttpPost("packages")]
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
        public async Task<IActionResult> PackagesPost()
        {
            Package package = null;

            try
            {
                // Parse Headers
                HeaderProcessor.ToDictionary(this.Request.Headers);

                // Parse body as package
                package = await Parser.StreamParser<Package>(this.Request.Body, this.logger);
                ApiDataValidator.Validate(package);

                // Add Package Store
                await this.dataStore.AddPackage(package);
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

            return new ApiObjectResult(new ApiResponse<Package>(package));
        }

        /// <summary>
        /// Get packages.
        /// </summary>
        /// <param name="packageIdentifier">Optional package identifier.</param>
        /// <returns>IActionResult.</returns>
        [HttpGet("packages/{packageIdentifier?}")]
        public async Task<IActionResult> PackagesGet(string packageIdentifier = null)
        {
            ApiDataPage<Package> packages;
            Dictionary<string, string> headers;

            try
            {
                // Parse Headers
                headers = HeaderProcessor.ToDictionary(this.Request.Headers);

                // Fetch Results
                packages = await this.dataStore.GetPackages(packageIdentifier, headers.GetValueOrDefault(QueryConstants.ContinuationToken));
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

            return packages.Items.Count switch
            {
                0 => new NoContentResult(),
                1 => new ApiObjectResult(new ApiResponse<Package>(packages.Items.First(), packages.ContinuationToken)),
                _ => new ApiObjectResult(new ApiResponse<List<Package>>(packages.Items.ToList(), packages.ContinuationToken)),
            };
        }

        /// <summary>
        /// Update a package.
        /// </summary>
        /// <param name="packageIdentifier">Package identifier.</param>
        /// <returns>IActionResult.</returns>
        [HttpPut("packages/{packageIdentifier}")]
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
        public async Task<IActionResult> PackagesPut(string packageIdentifier)
        {
            Package package = null;

            try
            {
                // Parse Headers
                HeaderProcessor.ToDictionary(this.Request.Headers);

                // Parse body as package
                package = await Parser.StreamParser<Package>(this.Request.Body, this.logger);
                ApiDataValidator.Validate(package);

                // Validate Versions Match
                if (package.PackageIdentifier != packageIdentifier)
                {
                    throw new InvalidArgumentException(
                        new InternalRestError(
                            ErrorConstants.PackageDoesNotMatchErrorCode,
                            ErrorConstants.PackageDoesNotMatchErrorMessage));
                }

                await this.dataStore.UpdatePackage(packageIdentifier, package);
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

            return new ApiObjectResult(new ApiResponse<Package>(package));
        }

        /// <summary>
        /// Delete a package.
        /// </summary>
        /// <param name="packageIdentifier">Package identifier.</param>
        /// <returns>IActionResult.</returns>
        [HttpDelete("packages/{packageIdentifier}")]
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
        public async Task<IActionResult> PackagesDelete(string packageIdentifier)
        {
            try
            {
                // Parse Headers
                HeaderProcessor.ToDictionary(this.Request.Headers);
                await this.dataStore.DeletePackage(packageIdentifier);
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
