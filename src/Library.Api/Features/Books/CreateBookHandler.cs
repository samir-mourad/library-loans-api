using Library.Api.Features.Audit;
using Library.Api.Infrastructure.Persistence;
using Library.Domain.Books;
using Library.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Features.Books;

/// <summary>
/// Creates a book and its <c>BookCreated</c> audit event in a single <c>SaveChanges</c>.
/// ISBN uniqueness is enforced by the unique index, so concurrent duplicates are rejected deterministically.
/// </summary>
public sealed class CreateBookHandler(LibraryDbContext dbContext, IAuditTrail auditTrail, TimeProvider clock)
{
    public async Task<Result<BookResponse>> HandleAsync(CreateBookRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var isbn = Isbn.Create(request.Isbn);
        if (isbn.IsFailure)
        {
            return isbn.Error!;
        }

        var created = Book.Create(request.Title, request.Author, isbn.Value, request.TotalCopies, clock.GetUtcNow());
        if (created.IsFailure)
        {
            return created.Error!;
        }

        var book = created.Value;
        dbContext.Books.Add(book);
        auditTrail.Record(AuditEntityTypes.Book, book.Id, AuditActions.BookCreated, new
        {
            book.Title,
            book.Author,
            Isbn = book.Isbn.Value,
            book.TotalCopies
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation(BookConfiguration.IsbnIndexName))
        {
            return BookErrors.DuplicateIsbn;
        }

        return BookResponse.From(book);
    }
}
