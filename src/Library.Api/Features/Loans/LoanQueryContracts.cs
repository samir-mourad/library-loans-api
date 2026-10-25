using System.ComponentModel.DataAnnotations;
using Library.Api.Features.Books;
using Library.Api.Infrastructure.Pagination;
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
    public static AvailabilityResponse From(BookResponse book)
    {
        ArgumentNullException.ThrowIfNull(book);

        return new AvailabilityResponse(
            book.Id,
            book.TotalCopies,
            book.AvailableCopies,
            book.TotalCopies - book.AvailableCopies,
            book.IsActive,
            book.IsActive && book.AvailableCopies > 0);
    }
}
