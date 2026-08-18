using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Aspire.PythonModelRunner.Frontend.Extensions;

internal static class EndpointRouteBuilderExtensions
{
    extension(IEndpointRouteBuilder builder)
    {
        public IEndpointRouteBuilder MapAuthenticationEndpoints()
        {
            builder.MapGet("/login", (string? returnUrl) =>
                TypedResults.Challenge(
                    new AuthenticationProperties { RedirectUri = returnUrl ?? "/" },
                    [OpenIdConnectDefaults.AuthenticationScheme]));

            builder.MapGet("/logout", () =>
                TypedResults.SignOut(
                    new AuthenticationProperties { RedirectUri = "/" },
                    [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));

            return builder;
        }
    }
}
