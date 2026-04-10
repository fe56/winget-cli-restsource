namespace Microsoft.WinGet.RestSource.Server.AppConfig
{
    using System.Threading.Tasks;
    using Microsoft.WindowsPackageManager.Rest.Diagnostics;
    using Microsoft.WinGet.RestSource.AppConfig;

    /// <summary>
    /// Simple app configuration that reads feature flags from appsettings.json
    /// instead of Azure App Configuration.
    /// </summary>
    public class SimpleAppConfig : IWinGetAppConfig
    {
        private readonly IConfiguration configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleAppConfig"/> class.
        /// </summary>
        /// <param name="configuration">Application configuration.</param>
        public SimpleAppConfig(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        /// <inheritdoc/>
        public Task<bool> IsEnabledAsync(FeatureFlag flag, LoggingContext? loggingContext)
        {
            return Task.FromResult(this.IsEnabled(flag, loggingContext));
        }

        /// <inheritdoc/>
        public bool IsEnabled(FeatureFlag flag, LoggingContext? loggingContext)
        {
            // Geneva logging is always disabled in self-hosted mode
            if (flag == FeatureFlag.GenevaLogging)
            {
                return false;
            }

            var value = this.configuration[$"FeatureFlags:{flag}"];
            return bool.TryParse(value, out var result) && result;
        }
    }
}
