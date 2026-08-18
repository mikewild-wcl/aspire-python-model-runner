using Aspire.PythonModelRunner.AppHost.Extensions;
using Aspire.PythonModelRunner.AppHost.ParameterDefaults;
using Aspire.PythonModelRunner.Shared;
using Microsoft.Extensions.Hosting;
using Scalar.Aspire;

var builder = DistributedApplication.CreateBuilder(
    new DistributedApplicationOptions
    {
        Args = args,
        DashboardApplicationName = ApplicationConstants.Name,
    });

builder.AddAzureContainerAppEnvironment(ResourceNames.AzureContainerEnvironment);

var useEntraAuthenticationParameter = builder.AddParameter(
    ParameterNames.UseEntraAuthentication,
    new BooleanParameterDefault());
var useEntraAuthentication = useEntraAuthenticationParameter.GetValue<bool>() ?? false;

//var serviceBus = builder.AddAzureServiceBus(ResourceNames.ServiceBus)
//    .RunAsEmulator(c => c
//        .WithLifetime(ContainerLifetime.Persistent));

// Run the Python FastAPI app and expose its HTTP endpoint externally.
// Allow insecure hosts for PyPI to enable package installation during build.
const string pythonAppModule = "aspire_python_model_runner.main:app";

var pythonApp = builder.AddUvicornApp(
        name: ResourceNames.PythonApp,
        appDirectory: "../../python/aspire-python-model-runner",
        app: pythonAppModule)
    .WithVirtualEnvironment(".venv", createIfNotExists: false)
    .WithUv(args: ["sync", "--allow-insecure-host", "pypi.org", "--allow-insecure-host", "files.pythonhosted.org"])
    .PublishAsDockerFile(container => container
        .WithArgs(pythonAppModule, "--host", "0.0.0.0", "--port", "8000"))
    .WithEnvironment("USE_ENTRA_AUTHENTICATION", useEntraAuthentication ? "true" : "false")
    //.WithReference(serviceBus)
    //.WaitFor(serviceBus)
    //.WithEnvironment("ENTRA_TENANT_ID", entraTenantId)
    //.WithEnvironment("ENTRA_CLIENT_ID", entraApiClientId)
    .WithHttpHealthCheck(path: "/health")
    .WithUrlForEndpoint("http", static url => url.DisplayText = "🐍 Python app (http)")
    .WithIconName("Poll"); // or Molecule, or Code.

if (!builder.ExecutionContext.IsPublishMode)
{
    // Allow ingress for testing in Azure
    pythonApp.WithExternalHttpEndpoints();
}

var backend = builder.AddProject<Projects.Backend>(ResourceNames.Backend)
    .WithFeatureFlag(useEntraAuthenticationParameter, FeatureFlags.UseEntraAuthentication)
    //.WithReference(serviceBus)
    //.WaitFor(serviceBus)
    .WithReference(pythonApp)
    .WaitFor(pythonApp)
    .WithUrlForEndpoint("http", static url => url.DisplayText = "☁ API (http)")
    .WithUrlForEndpoint("https", static url => url.DisplayText = "☁ API (https)")
    .WithIconName("Cloud");

// Add API Reference only in Development - it isn't supported for publish to Azure 
if (builder.Environment.IsDevelopment())
{
    builder.AddScalarApiReference(ResourceNames.Scalar, options => options.AllowSelfSignedCertificates())
        .WithApiReference(backend, configureOptions: options => options.PreferHttpsEndpoint());
}

var frontend = builder.AddProject<Projects.Frontend>(ResourceNames.Frontend)
    .WithFeatureFlag(useEntraAuthenticationParameter, FeatureFlags.UseEntraAuthentication)
    .WithReference(backend)
    .WaitFor(backend)
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("http", static url => url.DisplayText = "🏠 Home (http)")
    .WithUrlForEndpoint("https", static url => url.DisplayText = "🏠 Home (https)")
    .WithIconName("Globe");

builder.ConfigureAuthentication(frontend, backend, pythonApp, useEntraAuthentication);

await builder.Build().RunAsync().ConfigureAwait(true);
