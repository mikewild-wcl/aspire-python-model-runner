using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using Microsoft.Identity.Web;

namespace Aspire.PythonModelRunner.Frontend;

 [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by dependency injection.")]
internal sealed class BearerTokenHandler(
    ITokenAcquisition tokenAcquisition,
    IConfiguration configuration) : DelegatingHandler
{
    private readonly string[] _scopes =
    [
        $"api://{configuration["BackendApi:ClientId"] ?? throw new InvalidOperationException("BackendApi:ClientId configuration is required.")}/.default"
    ];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await tokenAcquisition.GetAccessTokenForUserAsync(_scopes);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
