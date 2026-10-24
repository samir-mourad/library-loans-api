using System.ComponentModel.DataAnnotations;
using Library.Api.Infrastructure.Pagination;
using Library.Domain.Books;
using Library.Domain.Loans;

namespace Library.Api.Features.Loans;

public sealed class LoanHistoryQuery
{
    public LoanStatus? Status { get; init; }

    [Range(1, Paging.MaxPage)]
    public int? Page { get; init; }

    [Range(1, Paging.MaxPageSize)]
    public int? PageSize { get; init; }
}

public sealed record AvailabilityResponse(
    Guid BookId,
    int TotalCopies,
    int AvailableCopies,
    int LoanedCopies,
    bool IsActive,
    bool IsAvailable)
{
    public static AvailabilityResponse From(Book book)
    {
        ArgumentNullException.ThrowIfNull(book);

        return new AvailabilityResponse(
            book.Id,
            book.TotalCopies,
            book.AvailableCopies,
            book.LoanedCopies,
            book.IsActive,
            book.IsActive && book.AvailableCopies > 0);
    }
}
