using Library.Api.Features.Audit;
using Library.Api.Infrastructure.Persistence;
using Library.Domain.Common;
using Library.Domain.Loans;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Features.Loans;

/// <summary>
/// Shared workflow for ending a loan (return or cancel) in one transaction:
/// lock the loan row, apply the domain transition, give the copy back and record the audit event.
/// Concurrent attempts on the same loan serialize on the row lock, so the copy is restored exactly once.
/// Locks are always taken loan → book, which cannot deadlock with loan creation (book only).
/// </summary>
public sealed class LoanCloser(
    LibraryDbContext dbContext,
    IAuditTrail auditTrail,
    TimeProvider clock,
    ILogger<LoanCloser> logger)
{
    public async Task<Result<LoanResponse>> CloseAsync(
        Guid loanId,
        Func<Loan, DateTimeOffset, Result<FieldChange>> transition,
        string auditAction,
        Func<Loan, FieldChange, object> auditData,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(auditData);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var loan = await dbContext.Loans
            .FromSql($"SELECT * FROM loans WHERE id = {loanId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (loan is null)
        {
            return LoanErrors.NotFound;
        }

        var now = clock.GetUtcNow();
        var change = transition(loan, now);
        if (change.IsFailure)
        {
            return change.Error!;
        }

        await RestoreCopyAsync(loan.BookId, cancellationToken);
        auditTrail.Record(AuditEntityTypes.Loan, loan.Id, auditAction, auditData(loan, change.Value));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        LoanLog.Closed(logger, loan.Id, loan.BookId, loan.Status);
        return LoanResponse.From(loan, now);
    }

    /// <summary>
    /// Gives one copy back, never above the total. Affecting no row means availability is already inconsistent;
    /// failing loudly (rollback + 500) is safer than silently hiding it.
    /// </summary>
    private async Task RestoreCopyAsync(Guid bookId, CancellationToken cancellationToken)
    {
        var restored = await dbContext.Books
            .Where(book => book.Id == bookId && book.AvailableCopies < book.TotalCopies)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(book => book.AvailableCopies, book => book.AvailableCopies + 1),
                cancellationToken);

        if (restored != 1)
        {
            throw new InvalidOperationException($"Availability of book {bookId} is inconsistent: no copy could be restored.");
        }
    }
}
