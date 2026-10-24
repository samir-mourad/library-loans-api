namespace Library.Api.Features.Audit;

/// <summary>
/// Records business audit events in the current unit of work, so they are committed atomically with the change they describe.
/// </summary>
public interface IAuditTrail
{
    void Record(string entityType, Guid entityId, string action, object data);
}
