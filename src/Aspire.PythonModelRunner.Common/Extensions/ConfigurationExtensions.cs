using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.PythonModelRunner.Common.Extensions;

[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Extension used from other assemblies.")]
public static class ConfigurationExtensions
{
    extension(IConfiguration configuration)
    {
        public bool? GetFeatureFlag(string key)
        {
            string? featureFlagValue;

            var featureFlag = configuration
                .GetSection("feature_management:feature_flags")
                .GetChildren()
                .FirstOrDefault(section =>
                    string.Equals(section["id"], key, StringComparison.OrdinalIgnoreCase));

            if (featureFlag is not null)
            {
                featureFlagValue = featureFlag["enabled"];
            }
            else
            {
                // Fall back to old-style feature flag
                featureFlagValue = configuration[$"FeatureManagement:{key}"];
            }

            return bool.TryParse(featureFlagValue, out var enabled)
                ? enabled
                : null;
        }
    }
}
