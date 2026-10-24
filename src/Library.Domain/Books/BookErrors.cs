using Library.Domain.Common;

namespace Library.Domain.Books;

public static class BookErrors
{
    public static readonly Error NotFound = Error.NotFound("book.not_found", "The book was not found.");

    public static readonly Error Inactive = Error.Conflict("book.inactive", "The book is inactive and cannot be changed.");

    public static readonly Error CopiesBelowLoaned = Error.Conflict(
        "book.copies_below_loaned",
        "The total number of copies cannot be lower than the number of copies currently on loan.");

    public static readonly Error DuplicateIsbn = Error.Conflict("isbn.duplicate", "A book with this ISBN already exists.");

    public static readonly Error InvalidIsbn = Error.Validation("isbn.invalid", "The ISBN is not a valid ISBN-10 or ISBN-13.");

    public static readonly Error InvalidTitle = Error.Validation(
        "book.title_invalid",
        $"The title must have 1 to {Book.TitleMaxLength} characters.");

    public static readonly Error InvalidAuthor = Error.Validation(
        "book.author_invalid",
        $"The author must have 1 to {Book.AuthorMaxLength} characters.");

    public static readonly Error InvalidTotalCopies = Error.Validation(
        "book.copies_invalid",
        $"The total number of copies must be between {Book.MinCopies} and {Book.MaxCopies}.");
}
