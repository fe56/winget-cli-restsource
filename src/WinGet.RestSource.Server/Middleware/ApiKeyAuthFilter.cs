namespace Microsoft.WinGet.RestSource.Server.Middleware
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>
    /// Action filter that validates API key for write operations.
    /// </summary>
    public class ApiKeyAuthFilter : IAuthorizationFilter
    {
        private const string ApiKeyHeaderName = "X-Api-Key";
        private readonly string expectedApiKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiKeyAuthFilter"/> class.
        /// </summary>
        /// <param name="configuration">App configuration.</param>
        public ApiKeyAuthFilter(IConfiguration configuration)
        {
            this.expectedApiKey = configuration["ApiSettings:ApiKey"] ?? string.Empty;
        }

        /// <summary>
        /// Validates the API key from the request header.
        /// </summary>
        /// <param name="context">Authorization filter context.</param>
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (string.IsNullOrEmpty(this.expectedApiKey))
            {
                // No API key configured — allow all requests
                return;
            }

            if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedKey)
                || providedKey.ToString() != this.expectedApiKey)
            {
                context.Result = new UnauthorizedObjectResult(new { error = "Invalid or missing API key." });
            }
        }
    }
}
