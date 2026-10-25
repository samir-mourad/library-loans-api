using Library.Api.Infrastructure.Persistence;
using Library.Domain.Books;
using Library.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Features.Books;

/// <summary>
/// Returns a book by id, including inactive ones, so deactivated titles and their history remain reachable.
/// Served from the book snapshot cache, falling back to PostgreSQL.
/// </summary>
public sealed class GetBookHandler(LibraryDbContext dbContext, BookCache bookCache)
{
    public async Task<Result<BookResponse>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var book = await bookCache.GetOrLoadAsync(id, LoadAsync, cancellationToken);

        return book is null ? BookErrors.NotFound : book;

        async Task<BookResponse?> LoadAsync(CancellationToken token)
        {
            var entity = await dbContext.Books
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == id, token);

            return entity is null ? null : BookResponse.From(entity);
        }
    }
}
