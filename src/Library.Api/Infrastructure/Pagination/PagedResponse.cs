namespace Library.Api.Infrastructure.Pagination;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
