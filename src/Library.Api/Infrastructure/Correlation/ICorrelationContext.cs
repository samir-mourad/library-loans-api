namespace Library.Api.Infrastructure.Correlation;

/// <summary>
/// Correlation identifier of the current request, shared by logs, traces, error responses and audit events.
/// </summary>
public interface ICorrelationContext
{
    string CorrelationId { get; }
}
