namespace Library.Api.Infrastructure.ErrorHandling;

/// <summary>
/// Error codes for cross-cutting failures that do not belong to a specific feature.
/// </summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "validation.failed";
    public const string RouteNotFound = "route.not_found";
    public const string MethodNotAllowed = "method.not_allowed";
    public const string InternalError = "internal.error";
    public const string ActorInvalid = "actor.invalid";
}
