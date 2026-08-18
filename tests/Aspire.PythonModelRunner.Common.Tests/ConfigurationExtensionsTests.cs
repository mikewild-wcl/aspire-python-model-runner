using Aspire.PythonModelRunner.Common.Extensions;
using Microsoft.Extensions.Configuration;

namespace Aspire.PythonModelRunner.Common.Tests;

public class ConfigurationExtensionsTests
{
    [Fact]
    public void GetFeatureFlag_ReturnsValueFromFeatureManagementConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureManagement:EnabledFeature"] = "true",
            })
            .Build();

        Assert.True(configuration.GetFeatureFlag("EnabledFeature"));
    }

    [Fact]
    public void GetFeatureFlag_ReturnsValueFromFeatureManagementFeatureFlags()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["feature_management:feature_flags:0:id"] = "NewFeature",
                ["feature_management:feature_flags:0:enabled"] = "false",
            })
            .Build();

        Assert.False(configuration.GetFeatureFlag("newfeature"));
    }

    [Fact]
    public void GetFeatureFlag_PrefersFeatureManagementFeatureFlags()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["feature_management:feature_flags:0:id"] = "Feature",
                ["feature_management:feature_flags:0:enabled"] = "false",
                ["FeatureManagement:Feature"] = "true",
            })
            .Build();

        Assert.False(configuration.GetFeatureFlag("Feature"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-boolean")]
    public void GetFeatureFlag_ReturnsNullForMissingOrInvalidValue(string? value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureManagement:Feature"] = value,
            })
            .Build();

        Assert.Null(configuration.GetFeatureFlag("Feature"));
    }
}
