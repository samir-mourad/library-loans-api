using Library.Domain.Common;

namespace Library.Domain.Loans;

public static class LoanErrors
{
    public static readonly Error NotFound = Error.NotFound("loan.not_found", "The loan was not found.");

    public static readonly Error NotActive = Error.Conflict("loan.not_active", "The loan is no longer active.");

    public static readonly Error DuplicateActive = Error.Conflict(
        "loan.duplicate_active",
        "The user already has an active loan of this book.");

    public static readonly Error InvalidCancellationReason = Error.Validation(
        "loan.cancellation_reason_invalid",
        $"The cancellation reason must have at most {Loan.CancellationReasonMaxLength} characters.");
}
