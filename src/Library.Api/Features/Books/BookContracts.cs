using System.ComponentModel.DataAnnotations;
using Library.Api.Infrastructure.Pagination;
using Library.Domain.Books;

namespace Library.Api.Features.Books;

public sealed class CreateBookRequest
{
    [Required, StringLength(Book.TitleMaxLength)]
    public string Title { get; init; } = string.Empty;

    [Required, StringLength(Book.AuthorMaxLength)]
    public string Author { get; init; } = string.Empty;

    [Required, StringLength(20)]
    public string Isbn { get; init; } = string.Empty;

    [Range(Book.MinCopies, Book.MaxCopies)]
    public int TotalCopies { get; init; }
}

/// <summary>
/// Partial update: omitted or null fields are left unchanged. The ISBN is immutable.
/// </summary>
public sealed class UpdateBookRequest
{
    [StringLength(Book.TitleMaxLength, MinimumLength = 1)]
    public string? Title { get; init; }

    [StringLength(Book.AuthorMaxLength, MinimumLength = 1)]
    public string? Author { get; init; }

    [Range(Book.MinCopies, Book.MaxCopies)]
    public int? TotalCopies { get; init; }
}

public sealed class ListBooksQuery
{
    [StringLength(100)]
    public string? Search { get; init; }

    [StringLength(20)]
    public string? Isbn { get; init; }

    public bool? IncludeInactive { get; init; }

    [Range(1, Paging.MaxPage)]
    public int? Page { get; init; }

    [Range(1, Paging.MaxPageSize)]
    public int? PageSize { get; init; }
}

public sealed record BookResponse(
    Guid Id,
    string Title,
    string Author,
    string Isbn,
    int TotalCopies,
    int AvailableCopies,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static BookResponse From(Book book)
    {
        ArgumentNullException.ThrowIfNull(book);

        return new BookResponse(
            book.Id,
            book.Title,
            book.Author,
            book.Isbn.Value,
            book.TotalCopies,
            book.AvailableCopies,
            book.IsActive,
            book.CreatedAt,
            book.UpdatedAt);
    }
}
