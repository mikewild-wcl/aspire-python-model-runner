using Aspire.PythonModelRunner.Backend;
using Aspire.PythonModelRunner.Backend.Extensions;
using Aspire.PythonModelRunner.Common.Extensions;
using Aspire.PythonModelRunner.Shared;
using Microsoft.FeatureManagement;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddFeatureManagement();

// Need to read configuration directly, because Feature Management is not available until after the DI container is built.
var useEntraAuthentication = builder.Configuration.GetFeatureFlag("UseEntraAuthentication")
                             ?? false;
if (useEntraAuthentication)
{
    builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration)
        .EnableTokenAcquisitionToCallDownstreamApi()
        .AddInMemoryTokenCaches();
    builder.Services.AddAuthorization();
    builder.Services.AddTransient<PythonBearerTokenHandler>();
}

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.AddAzureServiceBusClient(connectionName: ResourceNames.ServiceBus);

var pythonClient = builder.Services.AddHttpClient("Python", client =>
{
    client.BaseAddress = new Uri($"https+http://{ResourceNames.PythonApp}");
});

if (useEntraAuthentication)
{
    pythonClient.AddHttpMessageHandler<PythonBearerTokenHandler>();
}

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

if (useEntraAuthentication)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.RegisterApiEndpoints(useEntraAuthentication);

await app.RunAsync();
