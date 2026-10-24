using Library.Api.Infrastructure.Pagination;
using Library.Api.Infrastructure.Persistence;
using Library.Domain.Books;
using Library.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Features.Books;

/// <summary>
/// Lists books ordered by title, optionally filtered by text (title or author, case-insensitive) and exact ISBN.
/// Inactive books are hidden unless explicitly requested.
/// </summary>
public sealed class ListBooksHandler(LibraryDbContext dbContext)
{
    private const string LikeEscape = "\\";

    public async Task<Result<PagedResponse<BookResponse>>> HandleAsync(ListBooksQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var books = dbContext.Books.AsNoTracking();

        if (query.IncludeInactive is not true)
        {
            books = books.Where(book => book.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Isbn))
        {
            var isbn = Isbn.Create(query.Isbn);
            if (isbn.IsFailure)
            {
                return isbn.Error!;
            }

            books = books.Where(book => book.Isbn == isbn.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{EscapeLikePattern(query.Search.Trim())}%";
            books = books.Where(book =>
                EF.Functions.ILike(book.Title, pattern, LikeEscape) || EF.Functions.ILike(book.Author, pattern, LikeEscape));
        }

        var page = query.Page ?? Paging.DefaultPage;
        var pageSize = query.PageSize ?? Paging.DefaultPageSize;
        var totalCount = await books.CountAsync(cancellationToken);
        var items = await books
            .OrderBy(book => book.Title)
            .ThenBy(book => book.Id)
            .Skip(Paging.Offset(page, pageSize))
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<BookResponse>([.. items.Select(BookResponse.From)], page, pageSize, totalCount);
    }

    private static string EscapeLikePattern(string value) =>
        value.Replace(LikeEscape, LikeEscape + LikeEscape, StringComparison.Ordinal)
            .Replace("%", LikeEscape + "%", StringComparison.Ordinal)
            .Replace("_", LikeEscape + "_", StringComparison.Ordinal);
}
