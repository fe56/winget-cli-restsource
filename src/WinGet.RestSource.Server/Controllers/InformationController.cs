// -----------------------------------------------------------------------
// <copyright file="InformationController.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. Licensed under the MIT License.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.WinGet.RestSource.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Microsoft.WinGet.RestSource.Server.Common;
    using Microsoft.WinGet.RestSource.Utils.Common;
    using Microsoft.WinGet.RestSource.Utils.Exceptions;
    using Microsoft.WinGet.RestSource.Utils.Models;
    using Microsoft.WinGet.RestSource.Utils.Models.Schemas;

    /// <summary>
    /// Controller for server information endpoint.
    /// </summary>
    [ApiController]
    [Route("api")]
    public class InformationController : ControllerBase
    {
        private readonly ILogger<InformationController> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="InformationController"/> class.
        /// </summary>
        /// <param name="logger">Logger.</param>
        public InformationController(ILogger<InformationController> logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Get server information.
        /// </summary>
        /// <returns>IActionResult.</returns>
        [HttpGet("information")]
        public IActionResult InformationGet()
        {
            Information information = null;

            try
            {
                // Parse Headers
                HeaderProcessor.ToDictionary(this.Request.Headers);
                information = new Information();
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

            return new ApiObjectResult(new ApiResponse<Information>(information));
        }
    }
}
