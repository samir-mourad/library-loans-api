using Library.Api.Infrastructure.Persistence;
using Library.Domain.Books;
using Library.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Features.Books;

/// <summary>
/// Returns a book by id, including inactive ones, so deactivated titles and their history remain reachable.
/// </summary>
public sealed class GetBookHandler(LibraryDbContext dbContext)
{
    public async Task<Result<BookResponse>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var book = await dbContext.Books
            .AsNoTracking()
            .SingleOrDefaultAsync(book => book.Id == id, cancellationToken);

        return book is null ? BookErrors.NotFound : BookResponse.From(book);
    }
}
