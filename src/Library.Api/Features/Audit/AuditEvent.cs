namespace Library.Api.Features.Audit;

/// <summary>
/// Immutable business audit record. Rows are append-only, enforced by database triggers.
/// </summary>
public sealed class AuditEvent
{
    public required Guid Id { get; init; }

    public required string EntityType { get; init; }

    public required Guid EntityId { get; init; }

    public required string Action { get; init; }

    public required string Actor { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public required string CorrelationId { get; init; }

    /// <summary>
    /// JSON describing the change (snapshot or field-level diff), stored as <c>jsonb</c>.
    /// </summary>
    public required string Data { get; init; }
}
