namespace Aspire.PythonModelRunner.Backend.Extensions;

internal static class EndpointRouteBuilderExtensions
{
    extension(IEndpointRouteBuilder builder)
    {
        public IEndpointRouteBuilder RegisterApiEndpoints(bool useEntraAuthentication)
        {
            builder.MapGet("/api/v1/hello", () => new { Message = "hello" })
                .WithName("GetHello")
                .RequireAuthorizationWhen(useEntraAuthentication);

            builder.MapGet("/api/v1/python/hello", async (IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
                {
                    var client = httpClientFactory.CreateClient("Python");
                    using var response = await client.GetAsync(new Uri("api/v1/hello", UriKind.Relative), cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    return Results.Content(content, response.Content.Headers.ContentType?.MediaType ?? "application/json");
                })
                .WithName("GetPythonHello")
                .RequireAuthorizationWhen(useEntraAuthentication);
                
            //builder.MapPost("/api/v1/queue-message",
            //        async (ChatRequest request, IChatService chatService, CancellationToken cancellationToken) =>
            //        {
            //            if (string.IsNullOrWhiteSpace(request.Message))
            //            {
            //                return Results.BadRequest("Message is required.");
            //            }

            //            var response = await chatService.SendMessageAsync(request.Message, cancellationToken);
            //            return Results.Ok(new ChatResponse(response));
            //        })
            //    .WithName("PostQueueMessage")
            //    .RequireAuthorizationWhen(useEntraAuthentication);

            return builder;
        }
    }
}
