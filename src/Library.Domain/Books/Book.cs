using Library.Domain.Common;

namespace Library.Domain.Books;

/// <summary>
/// A catalog title with a number of physical copies. <see cref="AvailableCopies"/> is maintained atomically by
/// loans in the database; this entity guards the invariants of catalog changes.
/// </summary>
public sealed class Book
{
    public const int TitleMaxLength = 300;
    public const int AuthorMaxLength = 200;
    public const int MinCopies = 1;
    public const int MaxCopies = 10_000;

    private Book()
    {
        Title = string.Empty;
        Author = string.Empty;
        Isbn = null!;
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; }

    public string Author { get; private set; }

    public Isbn Isbn { get; private set; }

    public int TotalCopies { get; private set; }

    public int AvailableCopies { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public int LoanedCopies => TotalCopies - AvailableCopies;

    public static Result<Book> Create(string title, string author, Isbn isbn, int totalCopies, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(isbn);

        var error = ValidateTitle(title) ?? ValidateAuthor(author) ?? ValidateTotalCopies(totalCopies);
        if (error is not null)
        {
            return error;
        }

        return new Book
        {
            Id = Guid.CreateVersion7(now),
            Title = title.Trim(),
            Author = author.Trim(),
            Isbn = isbn,
            TotalCopies = totalCopies,
            AvailableCopies = totalCopies,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Applies the provided values (null means unchanged) and returns only the fields that actually changed.
    /// Changing the total adjusts available copies by the same delta and never drops below the copies on loan.
    /// Nothing is applied when any value is invalid.
    /// </summary>
    public Result<BookChanges> Update(string? title, string? author, int? totalCopies, DateTimeOffset now)
    {
        if (!IsActive)
        {
            return BookErrors.Inactive;
        }

        var error = (title is null ? null : ValidateTitle(title))
            ?? (author is null ? null : ValidateAuthor(author))
            ?? (totalCopies is null ? null : ValidateTotalCopies(totalCopies.Value));
        if (error is not null)
        {
            return error;
        }

        if (totalCopies < LoanedCopies)
        {
            return BookErrors.CopiesBelowLoaned;
        }

        var changes = new BookChanges();
        ApplyTitle(title?.Trim(), changes);
        ApplyAuthor(author?.Trim(), changes);
        ApplyTotalCopies(totalCopies, changes);

        if (changes.HasChanges)
        {
            UpdatedAt = now;
        }

        return changes;
    }

    /// <summary>
    /// Deactivates the book so it no longer accepts loans, preserving it and its history.
    /// Returns false when it was already inactive.
    /// </summary>
    public bool Deactivate(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return false;
        }

        IsActive = false;
        UpdatedAt = now;
        return true;
    }

    private void ApplyTitle(string? title, BookChanges changes)
    {
        if (title is null || title == Title)
        {
            return;
        }

        changes.Add("title", Title, title);
        Title = title;
    }

    private void ApplyAuthor(string? author, BookChanges changes)
    {
        if (author is null || author == Author)
        {
            return;
        }

        changes.Add("author", Author, author);
        Author = author;
    }

    private void ApplyTotalCopies(int? totalCopies, BookChanges changes)
    {
        if (totalCopies is null || totalCopies == TotalCopies)
        {
            return;
        }

        var newAvailable = AvailableCopies + (totalCopies.Value - TotalCopies);
        changes.Add("totalCopies", TotalCopies, totalCopies.Value);
        changes.Add("availableCopies", AvailableCopies, newAvailable);
        TotalCopies = totalCopies.Value;
        AvailableCopies = newAvailable;
    }

    private static Error? ValidateTitle(string title) =>
        IsWithinLength(title, TitleMaxLength) ? null : BookErrors.InvalidTitle;

    private static Error? ValidateAuthor(string author) =>
        IsWithinLength(author, AuthorMaxLength) ? null : BookErrors.InvalidAuthor;

    private static Error? ValidateTotalCopies(int totalCopies) =>
        totalCopies is >= MinCopies and <= MaxCopies ? null : BookErrors.InvalidTotalCopies;

    private static bool IsWithinLength(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength;
}
