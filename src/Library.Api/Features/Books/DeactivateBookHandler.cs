using Library.Api.Features.Audit;
using Library.Api.Infrastructure.Persistence;
using Library.Domain.Books;
using Library.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Features.Books;

/// <summary>
/// Soft-deletes a book under a row lock. Books are never physically removed, so their loan and audit history survive.
/// Deactivating an inactive book succeeds without recording a new event.
/// </summary>
public sealed class DeactivateBookHandler(LibraryDbContext dbContext, IAuditTrail auditTrail, TimeProvider clock)
{
    public async Task<Result> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var book = await dbContext.FindForUpdateAsync(id, cancellationToken);
        if (book is null)
        {
            return BookErrors.NotFound;
        }

        if (book.Deactivate(clock.GetUtcNow()))
        {
            auditTrail.Record(AuditEntityTypes.Book, book.Id, AuditActions.BookDeactivated, new
            {
                IsActive = new FieldChange(true, false)
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
