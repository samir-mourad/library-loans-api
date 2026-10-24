using System.ComponentModel.DataAnnotations;
using Library.Domain.Loans;

namespace Library.Api.Features.Loans;

public sealed class CreateLoanRequest
{
    [Required]
    public Guid BookId { get; init; }

    [Required]
    public Guid UserId { get; init; }
}

public sealed class CancelLoanRequest
{
    [StringLength(Loan.CancellationReasonMaxLength)]
    public string? Reason { get; init; }
}

public sealed record LoanResponse(
    Guid Id,
    Guid BookId,
    Guid UserId,
    LoanStatus Status,
    DateTimeOffset LoanedAt,
    DateTimeOffset DueAt,
    DateTimeOffset? ReturnedAt,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    bool IsOverdue)
{
    public static LoanResponse From(Loan loan, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(loan);

        return new LoanResponse(
            loan.Id,
            loan.BookId,
            loan.UserId,
            loan.Status,
            loan.LoanedAt,
            loan.DueAt,
            loan.ReturnedAt,
            loan.CancelledAt,
            loan.CancellationReason,
            loan.IsOverdue(now));
    }
}
