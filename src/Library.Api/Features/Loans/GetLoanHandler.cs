using Library.Api.Infrastructure.Persistence;
using Library.Domain.Common;
using Library.Domain.Loans;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Features.Loans;

public sealed class GetLoanHandler(LibraryDbContext dbContext, TimeProvider clock)
{
    public async Task<Result<LoanResponse>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var loan = await dbContext.Loans
            .AsNoTracking()
            .SingleOrDefaultAsync(loan => loan.Id == id, cancellationToken);

        return loan is null ? LoanErrors.NotFound : LoanResponse.From(loan, clock.GetUtcNow());
    }
}
