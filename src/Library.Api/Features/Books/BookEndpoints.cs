using Library.Api.Infrastructure.ErrorHandling;
using Library.Api.Infrastructure.Pagination;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Library.Api.Features.Books;

public static class BookEndpoints
{
    public static IEndpointRouteBuilder MapBookEndpoints(this IEndpointRouteBuilder app)
    {
        var books = app.MapGroup("/books").WithTags("Books");

        books.MapPost("/", CreateAsync).WithName("CreateBook");
        books.MapGet("/", ListAsync).WithName("ListBooks");
        books.MapGet("/{id:guid}", GetAsync).WithName("GetBook");
        books.MapPatch("/{id:guid}", UpdateAsync).WithName("UpdateBook");
        books.MapDelete("/{id:guid}", DeactivateAsync).WithName("DeactivateBook");

        return app;
    }

    private static async Task<Results<Created<BookResponse>, ProblemHttpResult>> CreateAsync(
        CreateBookRequest request,
        CreateBookHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request, cancellationToken);

        return result.IsSuccess
            ? TypedResults.Created($"/books/{result.Value.Id}", result.Value)
            : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<PagedResponse<BookResponse>>, ProblemHttpResult>> ListAsync(
        [AsParameters] ListBooksQuery query,
        ListBooksHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(query, cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<BookResponse>, ProblemHttpResult>> GetAsync(
        Guid id,
        GetBookHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<BookResponse>, ProblemHttpResult>> UpdateAsync(
        Guid id,
        UpdateBookRequest request,
        UpdateBookHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, request, cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeactivateAsync(
        Guid id,
        DeactivateBookHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, cancellationToken);

        return result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();
    }
}
