using Library.Api.Features.Audit;
using Library.Api.Infrastructure.Persistence;
using Library.Domain.Books;
using Library.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Features.Books;

/// <summary>
/// Applies a partial update under a row lock and records a <c>BookUpdated</c> event with the exact field diff.
/// A request that changes nothing produces no audit event.
/// </summary>
public sealed class UpdateBookHandler(LibraryDbContext dbContext, IAuditTrail auditTrail, TimeProvider clock, BookCache bookCache)
{
    public async Task<Result<BookResponse>> HandleAsync(Guid id, UpdateBookRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var book = await dbContext.FindForUpdateAsync(id, cancellationToken);
        if (book is null)
        {
            return BookErrors.NotFound;
        }

        var updated = book.Update(request.Title, request.Author, request.TotalCopies, clock.GetUtcNow());
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        if (updated.Value.HasChanges)
        {
            auditTrail.Record(AuditEntityTypes.Book, book.Id, AuditActions.BookUpdated, new { Changes = updated.Value.Fields });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        if (updated.Value.HasChanges)
        {
            await bookCache.InvalidateAsync(book.Id);
        }

        return BookResponse.From(book);
    }
}
