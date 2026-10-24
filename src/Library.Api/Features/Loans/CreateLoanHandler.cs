using Library.Api.Features.Audit;
using Library.Api.Infrastructure.Persistence;
using Library.Domain.Books;
using Library.Domain.Common;
using Library.Domain.Loans;
using Library.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Library.Api.Features.Loans;

/// <summary>
/// Lends a copy using a single conditional UPDATE as the decision point:
/// <c>available_copies = available_copies - 1 WHERE is_active AND available_copies &gt; 0</c>.
/// Under READ COMMITTED a concurrent transaction waits for the row lock and PostgreSQL re-evaluates the predicate on the
/// committed row, so the last copy is granted exactly once across any number of replicas, without retries.
/// The loan, its audit event and the decrement commit or roll back together.
/// </summary>
public sealed class CreateLoanHandler(
    LibraryDbContext dbContext,
    IAuditTrail auditTrail,
    TimeProvider clock,
    IOptions<LoansOptions> options,
    ILogger<CreateLoanHandler> logger)
{
    public async Task<Result<LoanResponse>> HandleAsync(CreateLoanRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (!await dbContext.Users.AnyAsync(user => user.Id == request.UserId, cancellationToken))
        {
            return Reject(request, UserErrors.NotFound);
        }

        if (!await TryReserveCopyAsync(request.BookId, cancellationToken))
        {
            return Reject(request, await DiagnoseRejectionAsync(request.BookId, cancellationToken));
        }

        var now = clock.GetUtcNow();
        var loan = Loan.Create(request.BookId, request.UserId, now, TimeSpan.FromDays(options.Value.DefaultPeriodDays));
        dbContext.Loans.Add(loan);
        auditTrail.Record(AuditEntityTypes.Loan, loan.Id, AuditActions.LoanCreated, new { loan.BookId, loan.UserId, loan.DueAt });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation(LoanConfiguration.ActiveLoanIndexName))
        {
            return Reject(request, LoanErrors.DuplicateActive);
        }

        await transaction.CommitAsync(cancellationToken);

        LoanLog.Created(logger, loan.Id, loan.BookId, loan.UserId);
        return LoanResponse.From(loan, now);
    }

    private async Task<bool> TryReserveCopyAsync(Guid bookId, CancellationToken cancellationToken)
    {
        var reserved = await dbContext.Books
            .Where(book => book.Id == bookId && book.IsActive && book.AvailableCopies > 0)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(book => book.AvailableCopies, book => book.AvailableCopies - 1),
                cancellationToken);

        return reserved == 1;
    }

    /// <summary>
    /// Explains a failed reservation. It only chooses the error message; the decision was already made atomically.
    /// </summary>
    private async Task<Error> DiagnoseRejectionAsync(Guid bookId, CancellationToken cancellationToken)
    {
        var isActive = await dbContext.Books
            .Where(book => book.Id == bookId)
            .Select(book => (bool?)book.IsActive)
            .SingleOrDefaultAsync(cancellationToken);

        return isActive switch
        {
            null => BookErrors.NotFound,
            false => BookErrors.Inactive,
            true => BookErrors.Unavailable
        };
    }

    private Error Reject(CreateLoanRequest request, Error error)
    {
        LoanLog.Rejected(logger, request.BookId, request.UserId, error.Code);
        return error;
    }
}
