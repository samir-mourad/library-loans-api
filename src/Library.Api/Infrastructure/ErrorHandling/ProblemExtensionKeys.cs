namespace Library.Api.Infrastructure.ErrorHandling;

/// <summary>
/// Names of the extension members added to every Problem Details response.
/// </summary>
public static class ProblemExtensionKeys
{
    public const string Code = "code";
    public const string CorrelationId = "correlationId";
    public const string TraceId = "traceId";
}
