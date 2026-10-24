using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Library.Api.Infrastructure.Pagination;

namespace Library.Api.Features.Audit;

public sealed class ListAuditEventsQuery
{
    [StringLength(50)]
    public string? EntityType { get; init; }

    public Guid? EntityId { get; init; }

    [StringLength(100)]
    public string? Action { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    [Range(1, Paging.MaxPage)]
    public int? Page { get; init; }

    [Range(1, Paging.MaxPageSize)]
    public int? PageSize { get; init; }
}

public sealed record AuditEventResponse(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string Action,
    string Actor,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    JsonElement Data)
{
    public static AuditEventResponse From(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        using var data = JsonDocument.Parse(auditEvent.Data);

        return new AuditEventResponse(
            auditEvent.Id,
            auditEvent.EntityType,
            auditEvent.EntityId,
            auditEvent.Action,
            auditEvent.Actor,
            auditEvent.OccurredAt,
            auditEvent.CorrelationId,
            data.RootElement.Clone());
    }
}
