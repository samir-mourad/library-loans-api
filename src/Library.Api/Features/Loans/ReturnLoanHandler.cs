using Library.Api.Features.Audit;
using Library.Domain.Common;

namespace Library.Api.Features.Loans;

public sealed class ReturnLoanHandler(LoanCloser loanCloser)
{
    public Task<Result<LoanResponse>> HandleAsync(Guid loanId, CancellationToken cancellationToken) =>
        loanCloser.CloseAsync(
            loanId,
            (loan, now) => loan.Return(now),
            AuditActions.LoanReturned,
            (loan, change) => new { Status = change, loan.BookId, loan.ReturnedAt },
            cancellationToken);
}
