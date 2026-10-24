using System.Diagnostics;
using Library.Api.Infrastructure.ErrorHandling;
using Library.Domain.Common;

namespace Library.Api.Infrastructure.Actor;

/// <summary>
/// Resolves the actor from <c>X-Actor-Id</c>: absent means <c>anonymous</c>; present but blank, longer than
/// 100 characters or containing control characters is rejected with 400 <c>actor.invalid</c>.
/// </summary>
internal sealed class ActorMiddleware(RequestDelegate next, ILogger<ActorMiddleware> logger)
{
    public const string HeaderName = "X-Actor-Id";
    private const int MaxLength = 100;

    private static readonly Error InvalidActor = Error.Validation(
        ErrorCodes.ActorInvalid,
        $"Header {HeaderName} must have 1 to {MaxLength} characters and no control characters.");

    public async Task InvokeAsync(HttpContext context, ActorContext actor)
    {
        if (!TryResolve(context.Request.Headers, out var actorId))
        {
            await InvalidActor.ToProblem().ExecuteAsync(context);
            return;
        }

        actor.ActorId = actorId;
        Activity.Current?.SetTag("enduser.id", actorId);

        using var scope = logger.BeginScope("actorId:{actorId}", actorId);
        await next(context);
    }

    private static bool TryResolve(IHeaderDictionary headers, out string actorId)
    {
        if (!headers.TryGetValue(HeaderName, out var values))
        {
            actorId = ActorContext.Anonymous;
            return true;
        }

        actorId = values.ToString().Trim();
        return actorId.Length is > 0 and <= MaxLength && !actorId.Any(char.IsControl);
    }
}
