using Library.Domain.Common;

namespace Library.Domain.Loans;

/// <summary>
/// A copy of a book lent to a user. Loans are never deleted: returning or cancelling only changes the status,
/// preserving the fact that the loan existed.
/// </summary>
public sealed class Loan
{
    public const int CancellationReasonMaxLength = 500;

    private Loan()
    {
    }

    public Guid Id { get; private set; }

    public Guid BookId { get; private set; }

    public Guid UserId { get; private set; }

    public LoanStatus Status { get; private set; }

    public DateTimeOffset LoanedAt { get; private set; }

    public DateTimeOffset DueAt { get; private set; }

    public DateTimeOffset? ReturnedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string? CancellationReason { get; private set; }

    /// <summary>
    /// Creates an active loan due after <paramref name="loanPeriod"/>. Copy availability is reserved atomically in the
    /// database before this is called.
    /// </summary>
    public static Loan Create(Guid bookId, Guid userId, DateTimeOffset now, TimeSpan loanPeriod)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(loanPeriod, TimeSpan.Zero);

        return new Loan
        {
            Id = Guid.CreateVersion7(now),
            BookId = bookId,
            UserId = userId,
            Status = LoanStatus.Active,
            LoanedAt = now,
            DueAt = now + loanPeriod
        };
    }

    public Result<FieldChange> Return(DateTimeOffset now)
    {
        if (Status != LoanStatus.Active)
        {
            return LoanErrors.NotActive;
        }

        ReturnedAt = now;
        return TransitionTo(LoanStatus.Returned);
    }

    /// <summary>
    /// Cancels an active loan (e.g. registered by mistake). A blank reason is stored as no reason.
    /// </summary>
    public Result<FieldChange> Cancel(DateTimeOffset now, string? reason)
    {
        if (Status != LoanStatus.Active)
        {
            return LoanErrors.NotActive;
        }

        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (normalizedReason?.Length > CancellationReasonMaxLength)
        {
            return LoanErrors.InvalidCancellationReason;
        }

        CancelledAt = now;
        CancellationReason = normalizedReason;
        return TransitionTo(LoanStatus.Cancelled);
    }

    public bool IsOverdue(DateTimeOffset now) => Status == LoanStatus.Active && now > DueAt;

    private FieldChange TransitionTo(LoanStatus status)
    {
        var change = new FieldChange(Status, status);
        Status = status;
        return change;
    }
}
