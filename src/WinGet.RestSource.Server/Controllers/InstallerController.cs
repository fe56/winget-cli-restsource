// -----------------------------------------------------------------------
// <copyright file="InstallerController.cs" company="Microsoft Corporation">
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
    /// Controller for installer operations.
    /// </summary>
    [ApiController]
    [Route("api")]
    public class InstallerController : ControllerBase
    {
        private readonly IApiDataStore dataStore;
        private readonly ILogger<InstallerController> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="InstallerController"/> class.
        /// </summary>
        /// <param name="dataStore">Data Store.</param>
        /// <param name="logger">Logger.</param>
        public InstallerController(IApiDataStore dataStore, ILogger<InstallerController> logger)
        {
            this.dataStore = dataStore;
            this.logger = logger;
        }

        /// <summary>
        /// Add an installer.
        /// </summary>
        /// <param name="packageIdentifier">Package identifier.</param>
        /// <param name="packageVersion">Package version.</param>
        /// <returns>IActionResult.</returns>
        [HttpPost("packages/{packageIdentifier}/versions/{packageVersion}/installers")]
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
        public async Task<IActionResult> InstallerPost(string packageIdentifier, string packageVersion)
        {
            Installer installer = null;

            try
            {
                // Parse Headers
                HeaderProcessor.ToDictionary(this.Request.Headers);

                // Parse body as installer
                installer = await Parser.StreamParser<Installer>(this.Request.Body, this.logger);
                ApiDataValidator.Validate(installer);

                await this.dataStore.AddInstaller(packageIdentifier, packageVersion, installer);
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

            return new ApiObjectResult(new ApiResponse<Installer>(installer));
        }

        /// <summary>
        /// Get installers.
        /// </summary>
        /// <param name="packageIdentifier">Package identifier.</param>
        /// <param name="packageVersion">Package version.</param>
        /// <param name="installerIdentifier">Optional installer identifier.</param>
        /// <returns>IActionResult.</returns>
        [HttpGet("packages/{packageIdentifier}/versions/{packageVersion}/installers/{installerIdentifier?}")]
        public async Task<IActionResult> InstallerGet(string packageIdentifier, string packageVersion, string installerIdentifier = null)
        {
            ApiDataPage<Installer> installers;

            try
            {
                // Parse Headers
                HeaderProcessor.ToDictionary(this.Request.Headers);

                installers = await this.dataStore.GetInstallers(packageIdentifier, packageVersion, installerIdentifier);
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

            return installers.Items.Count switch
            {
                0 => new NoContentResult(),
                1 => new ApiObjectResult(new ApiResponse<Installer>(installers.Items.First(), installers.ContinuationToken)),
                _ => new ApiObjectResult(new ApiResponse<List<Installer>>(installers.Items.ToList(), installers.ContinuationToken)),
            };
        }

        /// <summary>
        /// Update an installer.
        /// </summary>
        /// <param name="packageIdentifier">Package identifier.</param>
        /// <param name="packageVersion">Package version.</param>
        /// <param name="installerIdentifier">Installer identifier.</param>
        /// <returns>IActionResult.</returns>
        [HttpPut("packages/{packageIdentifier}/versions/{packageVersion}/installers/{installerIdentifier}")]
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
        public async Task<IActionResult> InstallerPut(string packageIdentifier, string packageVersion, string installerIdentifier)
        {
            Installer installer = null;

            try
            {
                // Parse Headers
                HeaderProcessor.ToDictionary(this.Request.Headers);

                // Parse body as installer
                installer = await Parser.StreamParser<Installer>(this.Request.Body, this.logger);
                ApiDataValidator.Validate(installer);

                if (installer.InstallerIdentifier != installerIdentifier)
                {
                    throw new InvalidArgumentException(
                        new InternalRestError(
                            ErrorConstants.InstallerDoesNotMatchErrorCode,
                            ErrorConstants.InstallerDoesNotMatchErrorMessage));
                }

                await this.dataStore.UpdateInstaller(packageIdentifier, packageVersion, installerIdentifier, installer);
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

            return new ApiObjectResult(new ApiResponse<Installer>(installer));
        }

        /// <summary>
        /// Delete an installer.
        /// </summary>
        /// <param name="packageIdentifier">Package identifier.</param>
        /// <param name="packageVersion">Package version.</param>
        /// <param name="installerIdentifier">Installer identifier.</param>
        /// <returns>IActionResult.</returns>
        [HttpDelete("packages/{packageIdentifier}/versions/{packageVersion}/installers/{installerIdentifier}")]
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
        public async Task<IActionResult> InstallerDelete(string packageIdentifier, string packageVersion, string installerIdentifier)
        {
            try
            {
                // Parse Headers
                HeaderProcessor.ToDictionary(this.Request.Headers);
                await this.dataStore.DeleteInstaller(packageIdentifier, packageVersion, installerIdentifier);
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
