namespace Library.Api.Features.Audit;

public static class AuditEntityTypes
{
    public const string Book = "Book";
}

public static class AuditActions
{
    public const string BookCreated = "BookCreated";
    public const string BookUpdated = "BookUpdated";
    public const string BookDeactivated = "BookDeactivated";
}
