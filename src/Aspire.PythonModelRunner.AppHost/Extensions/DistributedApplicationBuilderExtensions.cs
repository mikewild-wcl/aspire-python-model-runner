using Aspire.Hosting.Python;
using Aspire.PythonModelRunner.AppHost.ParameterDefaults;
using Aspire.PythonModelRunner.Shared;
using Microsoft.Extensions.Hosting;

namespace Aspire.PythonModelRunner.AppHost.Extensions;

internal static class DistributedApplicationBuilderExtensions
{
    extension(IDistributedApplicationBuilder builder)
    {
        internal async Task<bool> GetBooleanParameter(string parameterName)
        {
            var parameter = builder.AddParameter(parameterName, new BooleanParameterDefault());
            var parameterValue = await parameter.Resource.GetValueAsync(CancellationToken.None);
            var finalValue = bool.TryParse(parameterValue, out var value) && value;

            return finalValue;
        }

        internal IDistributedApplicationBuilder ConfigureAuthentication(
            IResourceBuilder<ProjectResource> frontend,
            IResourceBuilder<ProjectResource> backend,
            IResourceBuilder<UvicornAppResource> python,
            bool useEntraAuthentication = false,
            bool useManagedIdentity = false)
        {
            if (!useEntraAuthentication) return builder;

            var instance = builder.AddParameter("entra-instance", value: new EmptyParameterDefault());
            var tenantId = builder.AddParameter("entra-tenant-id", value: new EmptyParameterDefault());
            var clientId = builder.AddParameter("entra-client-id", value: new EmptyParameterDefault());
            var apiClientId = builder.AddParameter("entra-api-client-id", value: new EmptyParameterDefault());
            var pythonClientId = builder.AddParameter("entra-python-client-id", value: new EmptyParameterDefault());
            var clientSecret = builder.AddParameter("entra-client-secret", secret: true, value: new EmptyParameterDefault());
            var apiClientSecret = builder.AddParameter("entra-api-client-secret", secret: true, value: new EmptyParameterDefault());

            frontend
                .WithEnvironment("AzureAd__Instance", instance)
                .WithEnvironment("AzureAd__TenantId", tenantId)
                .WithEnvironment("AzureAd__ClientId", clientId)
                .WithEnvironment("BackendApi__ClientId", apiClientId)
                .WithEnvironment("AzureAd__ClientSecret", clientSecret);

            backend
                .WithEnvironment("AzureAd__Instance", instance)
                .WithEnvironment("AzureAd__TenantId", tenantId)
                .WithEnvironment("AzureAd__ClientId", apiClientId)
                .WithEnvironment("AzureAd__ClientSecret", apiClientSecret)
                .WithEnvironment("Python__ClientId", pythonClientId);

            python
                .WithEnvironment("AZUREAD__INSTANCE", instance)
                .WithEnvironment("AZUREAD__TENANTID", tenantId)
                .WithEnvironment("AZUREAD__CLIENTID", pythonClientId);

            if (useManagedIdentity && !builder.Environment.IsDevelopment())
            {
                // FederatedIdentity in production: use managed identity credential instead of client secret.
                var managedIdentityClientId = builder.AddParameter("entra-managed-identity-client-id", value: new EmptyParameterDefault());
                frontend
                    .WithEnvironment("AzureAd__ClientCredentials__0__SourceType", "SignedAssertionFromManagedIdentity")
                    .WithEnvironment("AzureAd__ClientCredentials__0__ManagedIdentityClientId", managedIdentityClientId);
            }

            return builder;
        }

        internal IDistributedApplicationBuilder ConfigureStorageConnections(
            ParameterResource storageQueueName,
            IResourceBuilder<ProjectResource> backend,
            IResourceBuilder<UvicornAppResource> python)
        {
            backend.WithEnvironment(EnvironmentVariableNames.StorageQueueName, storageQueueName);

            var storage = builder.AddAzureStorage(ResourceNames.AzureStorage)
                .RunAsEmulator();

            var queues = storage.AddQueues(ResourceNames.QueueStorage);

            backend
                .WithReference(queues)
                .WaitFor(queues);

            python
                .WithReference(queues)
                .WaitFor(queues);

            return builder;
        }
    }
}
