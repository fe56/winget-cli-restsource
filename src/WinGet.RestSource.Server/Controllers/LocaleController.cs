// -----------------------------------------------------------------------
// <copyright file="LocaleController.cs" company="Microsoft Corporation">
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
    /// Controller for locale operations.
    /// </summary>
    [ApiController]
    [Route("api")]
    public class LocaleController : ControllerBase
    {
        private readonly IApiDataStore dataStore;
        private readonly ILogger<LocaleController> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocaleController"/> class.
        /// </summary>
        /// <param name="dataStore">Data Store.</param>
        /// <param name="logger">Logger.</param>
        public LocaleController(IApiDataStore dataStore, ILogger<LocaleController> logger)
        {
            this.dataStore = dataStore;
            this.logger = logger;
        }

        /// <summary>
        /// Add a locale.
        /// </summary>
        /// <param name="packageIdentifier">Package identifier.</param>
        /// <param name="packageVersion">Package version.</param>
        /// <returns>IActionResult.</returns>
        [HttpPost("packages/{packageIdentifier}/versions/{packageVersion}/locales")]
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
        public async Task<IActionResult> LocalePost(string packageIdentifier, string packageVersion)
        {
            Locale locale = null;

            try
            {
                // Parse Headers
                HeaderProcessor.ToDictionary(this.Request.Headers);

                // Parse body as locale
                locale = await Parser.StreamParser<Locale>(this.Request.Body, this.logger);
                ApiDataValidator.Validate(locale);

                await this.dataStore.AddLocale(packageIdentifier, packageVersion, locale);
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

            return new ApiObjectResult(new ApiResponse<Locale>(locale));
        }

        /// <summary>
        /// Get locales.
        /// </summary>
        /// <param name="packageIdentifier">Package identifier.</param>
        /// <param name="packageVersion">Package version.</param>
        /// <param name="packageLocale">Optional locale.</param>
        /// <returns>IActionResult.</returns>
        [HttpGet("packages/{packageIdentifier}/versions/{packageVersion}/locales/{packageLocale?}")]
        public async Task<IActionResult> LocaleGet(string packageIdentifier, string packageVersion, string packageLocale = null)
        {
            ApiDataPage<Locale> locales;

            try
            {
                // Parse Headers
                HeaderProcessor.ToDictionary(this.Request.Headers);

                locales = await this.dataStore.GetLocales(packageIdentifier, packageVersion, packageLocale);
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

            return locales.Items.Count switch
            {
                0 => new NoContentResult(),
                1 => new ApiObjectResult(new ApiResponse<Locale>(locales.Items.First(), locales.ContinuationToken)),
                _ => new ApiObjectResult(new ApiResponse<List<Locale>>(locales.Items.ToList(), locales.ContinuationToken)),
            };
        }

        /// <summary>
        /// Update a locale.
        /// </summary>
        /// <param name="packageIdentifier">Package identifier.</param>
        /// <param name="packageVersion">Package version.</param>
        /// <param name="packageLocale">Locale.</param>
        /// <returns>IActionResult.</returns>
        [HttpPut("packages/{packageIdentifier}/versions/{packageVersion}/locales/{packageLocale}")]
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
        public async Task<IActionResult> LocalePut(string packageIdentifier, string packageVersion, string packageLocale)
        {
            Locale locale = null;

            try
            {
                // Parse Headers
                HeaderProcessor.ToDictionary(this.Request.Headers);

                // Parse body as locale
                locale = await Parser.StreamParser<Locale>(this.Request.Body, this.logger);
                ApiDataValidator.Validate(locale);

                if (locale.PackageLocale != packageLocale)
                {
                    throw new InvalidArgumentException(
                        new InternalRestError(
                            ErrorConstants.LocaleDoesNotMatchErrorCode,
                            ErrorConstants.LocaleDoesNotMatchErrorMessage));
                }

                await this.dataStore.UpdateLocale(packageIdentifier, packageVersion, packageLocale, locale);
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

            return new ApiObjectResult(new ApiResponse<Locale>(locale));
        }

        /// <summary>
        /// Delete a locale.
        /// </summary>
        /// <param name="packageIdentifier">Package identifier.</param>
        /// <param name="packageVersion">Package version.</param>
        /// <param name="packageLocale">Locale.</param>
        /// <returns>IActionResult.</returns>
        [HttpDelete("packages/{packageIdentifier}/versions/{packageVersion}/locales/{packageLocale}")]
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
        public async Task<IActionResult> LocaleDelete(string packageIdentifier, string packageVersion, string packageLocale)
        {
            try
            {
                // Parse Headers
                HeaderProcessor.ToDictionary(this.Request.Headers);
                await this.dataStore.DeleteLocale(packageIdentifier, packageVersion, packageLocale);
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
