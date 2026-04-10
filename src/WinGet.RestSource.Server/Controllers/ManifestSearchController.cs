// -----------------------------------------------------------------------
// <copyright file="ManifestSearchController.cs" company="Microsoft Corporation">
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
    using Microsoft.WinGet.RestSource.Utils.Common;
    using Microsoft.WinGet.RestSource.Utils.Constants;
    using Microsoft.WinGet.RestSource.Utils.Exceptions;
    using Microsoft.WinGet.RestSource.Utils.Models.Arrays;
    using Microsoft.WinGet.RestSource.Utils.Models.Schemas;
    using Microsoft.WinGet.RestSource.Utils.Validators;

    /// <summary>
    /// Controller for manifest search operations.
    /// </summary>
    [ApiController]
    [Route("api")]
    public class ManifestSearchController : ControllerBase
    {
        private readonly IApiDataStore dataStore;
        private readonly ILogger<ManifestSearchController> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ManifestSearchController"/> class.
        /// </summary>
        /// <param name="dataStore">Data Store.</param>
        /// <param name="logger">Logger.</param>
        public ManifestSearchController(IApiDataStore dataStore, ILogger<ManifestSearchController> logger)
        {
            this.dataStore = dataStore;
            this.logger = logger;
        }

        /// <summary>
        /// Search package manifests.
        /// </summary>
        /// <returns>IActionResult.</returns>
        [HttpPost("manifestSearch")]
        public async Task<IActionResult> ManifestSearchPost()
        {
            ApiDataPage<ManifestSearchResponse> manifestSearchResponse;
            PackageMatchFields unsupportedFields;
            PackageMatchFields requiredFields;
            ManifestSearchRequest manifestSearch = null;

            try
            {
                // Parse Headers
                Dictionary<string, string> headers = HeaderProcessor.ToDictionary(this.Request.Headers);
                string continuationToken = headers.GetValueOrDefault(HeaderConstants.ContinuationToken);

                // Get Manifest Search Request and Validate.
                manifestSearch = await Parser.StreamParser<ManifestSearchRequest>(this.Request.Body, this.logger);
                ApiDataValidator.Validate(manifestSearch);

                manifestSearchResponse = await this.dataStore.SearchPackageManifests(manifestSearch, continuationToken);

                unsupportedFields = UnsupportedAndRequiredFieldsHelper.GetUnsupportedPackageMatchFieldsFromSearchRequest(manifestSearch, ApiConstants.UnsupportedPackageMatchFields);
                requiredFields = UnsupportedAndRequiredFieldsHelper.GetRequiredPackageMatchFieldsFromSearchRequest(manifestSearch, ApiConstants.RequiredPackageMatchFields);
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

            return new ApiObjectResult(new SearchApiResponse<List<ManifestSearchResponse>>(manifestSearchResponse.Items?.ToList(), manifestSearchResponse.ContinuationToken)
            {
                UnsupportedPackageMatchFields = unsupportedFields,
                RequiredPackageMatchFields = requiredFields,
            });
        }
    }
}
