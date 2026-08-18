namespace Aspire.PythonModelRunner.Backend.Extensions;

internal static class RouteHandlerBuilderExtensions
{
    extension(RouteHandlerBuilder builder)
    {
        internal RouteHandlerBuilder RequireAuthorizationWhen(bool useEntraAuthentication)
        {
            if (useEntraAuthentication)
            {
                builder.RequireAuthorization();
            }

            return builder;
        }
    }
}
