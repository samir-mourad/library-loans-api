using Library.Api.Infrastructure.Persistence;
using Library.Domain.Books;
using Library.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Features.Loans;

/// <summary>
/// Current availability of a book, informational only: lending decisions are always made by the atomic update.
/// </summary>
public sealed class GetAvailabilityHandler(LibraryDbContext dbContext)
{
    public async Task<Result<AvailabilityResponse>> HandleAsync(Guid bookId, CancellationToken cancellationToken)
    {
        var book = await dbContext.Books
            .AsNoTracking()
            .SingleOrDefaultAsync(book => book.Id == bookId, cancellationToken);

        return book is null ? BookErrors.NotFound : AvailabilityResponse.From(book);
    }
}
