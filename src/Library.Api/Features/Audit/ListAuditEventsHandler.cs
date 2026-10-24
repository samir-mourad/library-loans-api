using Library.Api.Infrastructure.Pagination;
using Library.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Features.Audit;

/// <summary>
/// Queries the audit trail, newest first, filtered by entity, action and time window (<c>from</c> inclusive, <c>to</c> exclusive).
/// </summary>
public sealed class ListAuditEventsHandler(LibraryDbContext dbContext)
{
    public async Task<PagedResponse<AuditEventResponse>> HandleAsync(ListAuditEventsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var events = dbContext.AuditEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            events = events.Where(auditEvent => auditEvent.EntityType == query.EntityType);
        }

        if (query.EntityId is { } entityId)
        {
            events = events.Where(auditEvent => auditEvent.EntityId == entityId);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            events = events.Where(auditEvent => auditEvent.Action == query.Action);
        }

        if (query.From is { } from)
        {
            events = events.Where(auditEvent => auditEvent.OccurredAt >= from.ToUniversalTime());
        }

        if (query.To is { } to)
        {
            events = events.Where(auditEvent => auditEvent.OccurredAt < to.ToUniversalTime());
        }

        var page = query.Page ?? Paging.DefaultPage;
        var pageSize = query.PageSize ?? Paging.DefaultPageSize;
        var totalCount = await events.CountAsync(cancellationToken);
        var items = await events
            .OrderByDescending(auditEvent => auditEvent.OccurredAt)
            .ThenByDescending(auditEvent => auditEvent.Id)
            .Skip(Paging.Offset(page, pageSize))
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<AuditEventResponse>([.. items.Select(AuditEventResponse.From)], page, pageSize, totalCount);
    }
}
