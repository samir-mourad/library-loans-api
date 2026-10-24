namespace Library.Api.Infrastructure.Correlation;

/// <summary>
/// Request-scoped holder populated once by <see cref="CorrelationIdMiddleware"/>.
/// </summary>
internal sealed class CorrelationContext : ICorrelationContext
{
    public string CorrelationId { get; set; } = string.Empty;
}
