using Library.Api.Infrastructure.ErrorHandling;
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

        return app;
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
