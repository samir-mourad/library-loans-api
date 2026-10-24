using Library.Api.Infrastructure.Persistence;
using Library.Domain.Books;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Features.Books;

internal static class BookLocking
{
    /// <summary>
    /// Loads a book holding a row lock (<c>SELECT ... FOR UPDATE</c>) until the current transaction ends.
    /// Administrative writes use it to compute exact audit diffs and to serialize with concurrent loan updates.
    /// </summary>
    public static Task<Book?> FindForUpdateAsync(this LibraryDbContext dbContext, Guid id, CancellationToken cancellationToken) =>
        dbContext.Books
            .FromSql($"SELECT * FROM books WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
}
