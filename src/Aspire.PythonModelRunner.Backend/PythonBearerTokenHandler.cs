using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using Microsoft.Identity.Web;

namespace Aspire.PythonModelRunner.Backend;

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by dependency injection.")]
internal sealed class PythonBearerTokenHandler(
    ITokenAcquisition tokenAcquisition,
    IConfiguration configuration) : DelegatingHandler
{
    private readonly string[] _scopes =
    [
        $"api://{configuration["Python:ClientId"] ?? throw new InvalidOperationException("Python:ClientId configuration is required.")}/.default"
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
