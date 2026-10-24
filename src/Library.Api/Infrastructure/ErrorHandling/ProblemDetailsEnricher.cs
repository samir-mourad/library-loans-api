using Library.Api.Infrastructure.Correlation;

namespace Library.Api.Infrastructure.ErrorHandling;

/// <summary>
/// Adds <c>correlationId</c> to every Problem Details response and a default <c>code</c> when the producer did not set one.
/// </summary>
internal static class ProblemDetailsEnricher
{
    public static void Enrich(ProblemDetailsContext context)
    {
        var extensions = context.ProblemDetails.Extensions;
        var correlation = context.HttpContext.RequestServices.GetService<ICorrelationContext>();

        if (!string.IsNullOrEmpty(correlation?.CorrelationId))
        {
            extensions[ProblemExtensionKeys.CorrelationId] = correlation.CorrelationId;
        }

        var statusCode = context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode;
        extensions.TryAdd(ProblemExtensionKeys.Code, DefaultCodeFor(statusCode));
    }

    private static string DefaultCodeFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => ErrorCodes.ValidationFailed,
        StatusCodes.Status404NotFound => ErrorCodes.RouteNotFound,
        StatusCodes.Status405MethodNotAllowed => ErrorCodes.MethodNotAllowed,
        >= StatusCodes.Status500InternalServerError => ErrorCodes.InternalError,
        _ => $"http.{statusCode}"
    };
}
