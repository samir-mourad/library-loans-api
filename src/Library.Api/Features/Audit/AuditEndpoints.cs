using Library.Api.Infrastructure.Pagination;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Library.Api.Features.Audit;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/audit-events", ListAsync).WithTags("Audit").WithName("ListAuditEvents");
        return app;
    }

    private static async Task<Ok<PagedResponse<AuditEventResponse>>> ListAsync(
        [AsParameters] ListAuditEventsQuery query,
        ListAuditEventsHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(query, cancellationToken));
}
