namespace Library.Api.Infrastructure.Actor;

/// <summary>
/// Identity of who performed the current request, recorded in audit events.
/// Currently sourced from the <c>X-Actor-Id</c> header; replaceable by an authenticated principal without changing consumers.
/// </summary>
public interface IActorAccessor
{
    string ActorId { get; }
}
