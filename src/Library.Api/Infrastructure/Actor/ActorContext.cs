namespace Library.Api.Infrastructure.Actor;

/// <summary>
/// Request-scoped holder populated once by <see cref="ActorMiddleware"/>.
/// </summary>
internal sealed class ActorContext : IActorAccessor
{
    public const string Anonymous = "anonymous";

    public string ActorId { get; set; } = Anonymous;
}
