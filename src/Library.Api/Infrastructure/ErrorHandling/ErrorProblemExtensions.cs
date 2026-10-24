using Library.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Library.Api.Infrastructure.ErrorHandling;

/// <summary>
/// Translates domain <see cref="Error"/> values into RFC 9457 Problem Details responses.
/// </summary>
public static class ErrorProblemExtensions
{
    public static ProblemHttpResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var (statusCode, title) = Describe(error.Type);

        return TypedResults.Problem(
            detail: error.Message,
            statusCode: statusCode,
            title: title,
            extensions: new Dictionary<string, object?> { [ProblemExtensionKeys.Code] = error.Code });
    }

    public static int StatusCode(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return Describe(error.Type).StatusCode;
    }

    private static (int StatusCode, string Title) Describe(ErrorType type) => type switch
    {
        ErrorType.Validation => (StatusCodes.Status400BadRequest, "The request is invalid."),
        ErrorType.NotFound => (StatusCodes.Status404NotFound, "The resource was not found."),
        ErrorType.Conflict => (StatusCodes.Status409Conflict, "The request conflicts with the current state."),
        ErrorType.Unprocessable => (StatusCodes.Status422UnprocessableEntity, "The request cannot be processed."),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown error type.")
    };
}
