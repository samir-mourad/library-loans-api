using Library.Api.Infrastructure.ErrorHandling;
using Library.Api.Infrastructure.Pagination;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Library.Api.Features.Loans;

public static class LoanEndpoints
{
    public static IEndpointRouteBuilder MapLoanEndpoints(this IEndpointRouteBuilder app)
    {
        var loans = app.MapGroup("/loans").WithTags("Loans");

        loans.MapPost("/", CreateAsync).WithName("CreateLoan");
        loans.MapGet("/{id:guid}", GetAsync).WithName("GetLoan");
        loans.MapPost("/{id:guid}/return", ReturnAsync).WithName("ReturnLoan");
        loans.MapPost("/{id:guid}/cancel", CancelAsync).WithName("CancelLoan");

        app.MapGet("/books/{id:guid}/availability", GetAvailabilityAsync).WithTags("Books").WithName("GetBookAvailability");
        app.MapGet("/books/{id:guid}/history", GetBookHistoryAsync).WithTags("Books").WithName("GetBookLoanHistory");
        app.MapGet("/users/{id:guid}/loans", GetUserLoansAsync).WithTags("Users").WithName("GetUserLoans");

        return app;
    }

    private static async Task<Results<Ok<AvailabilityResponse>, ProblemHttpResult>> GetAvailabilityAsync(
        Guid id,
        GetAvailabilityHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<PagedResponse<LoanResponse>>, ProblemHttpResult>> GetBookHistoryAsync(
        Guid id,
        [AsParameters] LoanHistoryQuery query,
        LoanHistoryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ByBookAsync(id, query, cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<PagedResponse<LoanResponse>>, ProblemHttpResult>> GetUserLoansAsync(
        Guid id,
        [AsParameters] LoanHistoryQuery query,
        LoanHistoryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ByUserAsync(id, query, cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<LoanResponse>, ProblemHttpResult>> ReturnAsync(
        Guid id,
        ReturnLoanHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<LoanResponse>, ProblemHttpResult>> CancelAsync(
        Guid id,
        CancelLoanRequest? request,
        CancelLoanHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, request, cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Results<Created<LoanResponse>, ProblemHttpResult>> CreateAsync(
        CreateLoanRequest request,
        CreateLoanHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request, cancellationToken);

        return result.IsSuccess
            ? TypedResults.Created($"/loans/{result.Value.Id}", result.Value)
            : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<LoanResponse>, ProblemHttpResult>> GetAsync(
        Guid id,
        GetLoanHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }
}
