namespace Library.Api.Features.Audit;

public static class AuditEntityTypes
{
    public const string Book = "Book";
    public const string User = "User";
    public const string Loan = "Loan";
}

public static class AuditActions
{
    public const string BookCreated = "BookCreated";
    public const string BookUpdated = "BookUpdated";
    public const string BookDeactivated = "BookDeactivated";
    public const string UserCreated = "UserCreated";
    public const string LoanCreated = "LoanCreated";
    public const string LoanReturned = "LoanReturned";
    public const string LoanCancelled = "LoanCancelled";
}
