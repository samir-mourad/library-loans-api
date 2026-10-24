using Library.Api.Infrastructure.Pagination;
using Library.Api.Infrastructure.Persistence;
using Library.Domain.Books;
using Library.Domain.Common;
using Library.Domain.Loans;
using Library.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Features.Loans;

/// <summary>
/// Loan history by book or by user, newest first, including returned and cancelled loans.
/// </summary>
public sealed class LoanHistoryHandler(LibraryDbContext dbContext, TimeProvider clock)
{
    public async Task<Result<PagedResponse<LoanResponse>>> ByBookAsync(Guid bookId, LoanHistoryQuery query, CancellationToken cancellationToken)
    {
        if (!await dbContext.Books.AnyAsync(book => book.Id == bookId, cancellationToken))
        {
            return BookErrors.NotFound;
        }

        return await PageAsync(dbContext.Loans.Where(loan => loan.BookId == bookId), query, cancellationToken);
    }

    public async Task<Result<PagedResponse<LoanResponse>>> ByUserAsync(Guid userId, LoanHistoryQuery query, CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return UserErrors.NotFound;
        }

        return await PageAsync(dbContext.Loans.Where(loan => loan.UserId == userId), query, cancellationToken);
    }

    private async Task<PagedResponse<LoanResponse>> PageAsync(IQueryable<Loan> loans, LoanHistoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Status is { } status)
        {
            loans = loans.Where(loan => loan.Status == status);
        }

        var page = query.Page ?? Paging.DefaultPage;
        var pageSize = query.PageSize ?? Paging.DefaultPageSize;
        var totalCount = await loans.CountAsync(cancellationToken);
        var items = await loans
            .AsNoTracking()
            .OrderByDescending(loan => loan.LoanedAt)
            .ThenByDescending(loan => loan.Id)
            .Skip(Paging.Offset(page, pageSize))
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var now = clock.GetUtcNow();
        return new PagedResponse<LoanResponse>([.. items.Select(loan => LoanResponse.From(loan, now))], page, pageSize, totalCount);
    }
}
