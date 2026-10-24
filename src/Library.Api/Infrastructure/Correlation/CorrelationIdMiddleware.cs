using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Library.Api.Infrastructure.Correlation;

/// <summary>
/// Resolves the request correlation id from <c>X-Correlation-Id</c> (falling back to the trace id when absent or invalid),
/// exposes it to the request scope, logs and traces, and echoes it in the response header.
/// </summary>
internal sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context, CorrelationContext correlation)
    {
        var correlationId = Resolve(context.Request.Headers[HeaderName].ToString());

        correlation.CorrelationId = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using var scope = logger.BeginScope("correlationId:{correlationId}", correlationId);
        await next(context);
    }

    private static string Resolve(string candidate) =>
        ValidFormat().IsMatch(candidate)
            ? candidate
            : Activity.Current?.TraceId.ToHexString() ?? Guid.CreateVersion7().ToString("N");

    [GeneratedRegex("^[A-Za-z0-9._:-]{1,128}$")]
    private static partial Regex ValidFormat();
}
