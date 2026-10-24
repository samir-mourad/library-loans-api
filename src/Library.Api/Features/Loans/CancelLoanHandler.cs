using Library.Api.Features.Audit;
using Library.Domain.Common;

namespace Library.Api.Features.Loans;

/// <summary>
/// Cancels an active loan (e.g. registered by mistake), giving the copy back while preserving the loan record.
/// </summary>
public sealed class CancelLoanHandler(LoanCloser loanCloser)
{
    public Task<Result<LoanResponse>> HandleAsync(Guid loanId, CancelLoanRequest? request, CancellationToken cancellationToken) =>
        loanCloser.CloseAsync(
            loanId,
            (loan, now) => loan.Cancel(now, request?.Reason),
            AuditActions.LoanCancelled,
            (loan, change) => new { Status = change, loan.BookId, Reason = loan.CancellationReason },
            cancellationToken);
}
