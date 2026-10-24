using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Library.Api.Infrastructure.Health;

/// <summary>
/// Writes a compact health report with per-check status and duration, never exposing exception messages or connection details.
/// </summary>
internal static class HealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        var response = new HealthResponse(
            report.Status.ToString(),
            [.. report.Entries.Select(entry => new HealthCheckEntry(
                entry.Key,
                entry.Value.Status.ToString(),
                Math.Round(entry.Value.Duration.TotalMilliseconds, 2)))]);

        return context.Response.WriteAsJsonAsync(response, context.RequestAborted);
    }

    private sealed record HealthResponse(string Status, IReadOnlyList<HealthCheckEntry> Checks);

    private sealed record HealthCheckEntry(string Name, string Status, double DurationMs);
}
