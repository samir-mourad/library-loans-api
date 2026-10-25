using Library.Api.Features.Books;
using Library.Domain.Books;
using Library.Domain.Common;

namespace Library.Api.Features.Loans;

/// <summary>
/// Current availability of a book, derived from the cached book snapshot. Informational only: lending decisions are
/// always made by the atomic update in PostgreSQL.
/// </summary>
public sealed class GetAvailabilityHandler(GetBookHandler getBookHandler)
{
    public async Task<Result<AvailabilityResponse>> HandleAsync(Guid bookId, CancellationToken cancellationToken)
    {
        var book = await getBookHandler.HandleAsync(bookId, cancellationToken);

        return book.IsSuccess ? AvailabilityResponse.From(book.Value) : book.Error ?? BookErrors.NotFound;
    }
}
