using Aspire.Hosting;
using Aspire.PythonModelRunner.AppHost.Extensions;
using Aspire.PythonModelRunner.AppHost.Tests.TestExtensions;

namespace Aspire.PythonModelRunner.AppHost.Tests;

public class ResourceBuilderExtensionsTests
{
    [Fact]
    public void GetValue_ReturnsParameterValue()
    {
        var builder = DistributedApplication.CreateBuilder();
        var parameter = builder.AddParameter("feature", "true");

        Assert.Equal("true", parameter.GetValue());
    }

    [Fact]
    public void GetValue_ReturnsNullWhenParameterHasNoValue()
    {
        var builder = DistributedApplication.CreateBuilder();
        var parameter = builder.AddParameter("feature", () => null!);

        Assert.Null(parameter.GetValue());
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("TRUE", true)]
    [InlineData("not-a-boolean", null)]
    public void GetValue_ReturnsParsedBooleanValue(string value, bool? expected)
    {
        var builder = DistributedApplication.CreateBuilder();
        var parameter = builder.AddParameter("feature", value);

        var result = parameter.GetValue<bool>();
        Assert.Equal(expected, result);

        Assert.Equal(expected, parameter.GetValue<bool>());
    }

    [Fact]
    public async Task WithFeatureFlag_UsesNewStyleConfigurationByDefault()
    {
        var builder = DistributedApplication.CreateBuilder();
        var parameter = builder.AddParameter("feature", "true");
        var project = builder.AddProject<Projects.Backend>("backend");

        var result = project.WithFeatureFlag(parameter, "UseFeature");  
        var environment = await builder.GetResourceEnvironmentVariablesAsync(result.Resource);

        Assert.Same(project, result);
        Assert.Equal("UseFeature", environment["feature_management__feature_flags__0__id"]);
        Assert.Equal("true", environment["feature_management__feature_flags__0__enabled"]);
    }

    [Fact]
    public async Task WithFeatureFlag_UsesParameterNameAndOldStyleConfigurationWhenRequested()
    {
        var builder = DistributedApplication.CreateBuilder();
        var parameter = builder.AddParameter("feature", "false");
        var project = builder.AddProject<Projects.Backend>("backend");

        project.WithFeatureFlag(parameter, useNewStyleConfiguration: false);
        var environment = await builder.GetResourceEnvironmentVariablesAsync(project.Resource);

        Assert.Equal("false", environment["FeatureManagement:feature"]);
        Assert.DoesNotContain("feature_management__feature_flags__0__id", environment.Keys);
    }

    [Fact]
    public async Task WithFeatureFlag_DoesNotAddEnvironmentVariableWhenParameterHasNoValue()
    {
        var builder = DistributedApplication.CreateBuilder();
        var parameter = builder.AddParameter("feature", () => null!);
        var project = builder.AddProject<Projects.Backend>("backend");

        project.WithFeatureFlag(parameter);

        var environment = //await GetEnvironmentVariablesAsync(builder, project.Resource);
            await builder.GetResourceEnvironmentVariablesAsync(project.Resource);


        Assert.DoesNotContain("feature_management__feature_flags__0__id", environment.Keys);
        Assert.DoesNotContain("FeatureManagement:feature", environment.Keys);
    }

    [Fact]
    public async Task ConfigureAuthentication_ProvidesPythonEntraConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder();
        var frontend = builder.AddProject<Projects.Frontend>("frontend");
        var backend = builder.AddProject<Projects.Backend>("backend");
        var python = builder.AddUvicornApp("python", ".", "example:app");

        builder.ConfigureAuthentication(frontend, backend, python, useEntraAuthentication: true);

        var environment = await builder.GetResourceEnvironmentVariablesAsync(python.Resource);

        Assert.Contains("AZUREAD__INSTANCE", environment.Keys);
        Assert.Contains("AZUREAD__TENANTID", environment.Keys);
        Assert.Contains("AZUREAD__CLIENTID", environment.Keys);
    }
}
