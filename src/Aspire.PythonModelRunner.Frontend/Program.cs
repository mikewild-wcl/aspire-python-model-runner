using Aspire.PythonModelRunner.Common.Extensions;
using Aspire.PythonModelRunner.Frontend;
using Aspire.PythonModelRunner.Frontend.Components;
using Aspire.PythonModelRunner.Frontend.Extensions;
using Aspire.PythonModelRunner.Shared;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.FeatureManagement;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddFeatureManagement();

// Need to read configuration directly, because Feature Management is not available until after the DI container is built.
var useEntraAuthentication = builder.Configuration.GetFeatureFlag("UseEntraAuthentication");
if (useEntraAuthentication is true)
{
    builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
        .EnableTokenAcquisitionToCallDownstreamApi()
        .AddInMemoryTokenCaches();

    builder.Services.AddAuthorization();
    builder.Services.AddCascadingAuthenticationState();
    builder.Services.AddTransient<BearerTokenHandler>();
    builder.Services.AddControllersWithViews()
        .AddMicrosoftIdentityUI();
}
else
{
    builder.Services.AddAuthorization(options =>
    {
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true)
            .Build();
    });
}

var backendClient = builder.Services.AddHttpClient("Backend", client =>
{
    client.BaseAddress = new Uri($"https+http://{ResourceNames.Backend}");
});

if (useEntraAuthentication is true)
{
    backendClient.AddHttpMessageHandler<BearerTokenHandler>();
}

var razorComponents = builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddMicrosoftIdentityConsentHandler();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

if (useEntraAuthentication is true)
{
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapAuthenticationEndpoints();
    app.MapControllers();
}
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
