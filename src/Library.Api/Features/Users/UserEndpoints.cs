using Library.Api.Infrastructure.ErrorHandling;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Library.Api.Features.Users;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/users").WithTags("Users");

        users.MapPost("/", CreateAsync).WithName("CreateUser");
        users.MapGet("/{id:guid}", GetAsync).WithName("GetUser");

        return app;
    }

    private static async Task<Results<Created<UserResponse>, ProblemHttpResult>> CreateAsync(
        CreateUserRequest request,
        CreateUserHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request, cancellationToken);

        return result.IsSuccess
            ? TypedResults.Created($"/users/{result.Value.Id}", result.Value)
            : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<UserResponse>, ProblemHttpResult>> GetAsync(
        Guid id,
        GetUserHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }
}
