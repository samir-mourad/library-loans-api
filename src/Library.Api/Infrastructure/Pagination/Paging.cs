namespace Library.Api.Infrastructure.Pagination;

/// <summary>
/// Offset pagination limits. Page is capped so the computed offset never overflows.
/// </summary>
public static class Paging
{
    public const int DefaultPage = 1;
    public const int MaxPage = 100_000;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static int Offset(int page, int pageSize) => (page - 1) * pageSize;
}
