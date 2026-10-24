namespace Library.Domain.Loans;

/// <summary>
/// Lifecycle of a loan. <see cref="Returned"/> and <see cref="Cancelled"/> are terminal; overdue is derived, not a status.
/// </summary>
public enum LoanStatus
{
    Active,
    Returned,
    Cancelled
}
