using Library.Api.Infrastructure.ErrorHandling;
using Library.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Library.IntegrationTests.Infrastructure;

/// <summary>
/// Routes that exist only in tests, used to exercise cross-cutting behavior without depending on business features.
/// </summary>
internal static class TestEndpoints
{
    public const string Ok = "/_test/ok";
    public const string Throw = "/_test/throw";
    public const string ErrorsPrefix = "/_test/errors/";
    public const string SensitiveMessage = "sensitive internal detail";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Ok, () => TypedResults.Ok());
        endpoints.MapGet(Throw, IResult () => throw new InvalidOperationException(SensitiveMessage));
        endpoints.MapGet(ErrorsPrefix + "{type}", (ErrorType type) =>
            new Error(ErrorCodeFor(type), "Test error.", type).ToProblem());
    }

    public static string ErrorCodeFor(ErrorType type) => $"test.{type.ToString().ToLowerInvariant()}";
}
