using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.PythonModelRunner.AppHost.Tests.TestExtensions;

internal static class DistributedApplicationBuilderExtensions
{
    extension(IDistributedApplicationBuilder builder)
    {
        internal async Task<Dictionary<string, string>> GetResourceEnvironmentVariablesAsync(IResourceWithEnvironment resource)
        {
            await using var serviceProvider = builder.Services.BuildServiceProvider();
            var executionContext = new DistributedApplicationExecutionContext(
                new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Publish)
                {
                    Services = serviceProvider,
                });

            return (await ExecutionConfigurationBuilder
                .Create(resource)
                .WithEnvironmentVariablesConfig()
                .BuildAsync(executionContext)).EnvironmentVariables.ToDictionary(
                static variable => variable.Key,
                static variable => variable.Value);
        }
    }
}
