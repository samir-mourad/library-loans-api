using Library.Domain.Books;
using Library.Domain.Common;

namespace Library.UnitTests.Books;

public class BookTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = CreatedAt.AddHours(1);
    private static readonly Isbn SampleIsbn = Isbn.Create("9780306406157").Value;

    [Fact]
    public void Create_starts_active_with_all_copies_available()
    {
        var result = Book.Create("  Clean Code ", " Robert C. Martin ", SampleIsbn, 3, CreatedAt);

        var book = result.Value;
        Assert.Equal("Clean Code", book.Title);
        Assert.Equal("Robert C. Martin", book.Author);
        Assert.Equal(3, book.TotalCopies);
        Assert.Equal(3, book.AvailableCopies);
        Assert.True(book.IsActive);
        Assert.Equal(CreatedAt, book.CreatedAt);
        Assert.Equal(CreatedAt, book.UpdatedAt);
    }

    [Theory]
    [InlineData("", "Author", 1, "book.title_invalid")]
    [InlineData("   ", "Author", 1, "book.title_invalid")]
    [InlineData("Title", "", 1, "book.author_invalid")]
    [InlineData("Title", "Author", 0, "book.copies_invalid")]
    [InlineData("Title", "Author", 10_001, "book.copies_invalid")]
    public void Create_rejects_invalid_values(string title, string author, int totalCopies, string expectedCode)
    {
        var result = Book.Create(title, author, SampleIsbn, totalCopies, CreatedAt);

        Assert.Equal(expectedCode, result.Error?.Code);
    }

    [Fact]
    public void Create_rejects_title_longer_than_maximum()
    {
        var result = Book.Create(new string('a', Book.TitleMaxLength + 1), "Author", SampleIsbn, 1, CreatedAt);

        Assert.Equal(BookErrors.InvalidTitle, result.Error);
    }

    [Fact]
    public void Update_reports_only_changed_fields()
    {
        var book = NewBook(totalCopies: 2);

        var result = book.Update("New Title", "Original Author", null, Later);

        var change = Assert.Single(result.Value.Fields);
        Assert.Equal("title", change.Key);
        Assert.Equal(new FieldChange("Original Title", "New Title"), change.Value);
        Assert.Equal(Later, book.UpdatedAt);
    }

    [Fact]
    public void Update_without_changes_keeps_book_untouched()
    {
        var book = NewBook(totalCopies: 2);

        var result = book.Update("Original Title", null, 2, Later);

        Assert.False(result.Value.HasChanges);
        Assert.Equal(CreatedAt, book.UpdatedAt);
    }

    [Fact]
    public void Increasing_total_copies_increases_available_copies()
    {
        var book = NewBook(totalCopies: 2);

        var result = book.Update(null, null, 5, Later);

        Assert.Equal(5, book.TotalCopies);
        Assert.Equal(5, book.AvailableCopies);
        Assert.Equal(new FieldChange(2, 5), result.Value.Fields["totalCopies"]);
        Assert.Equal(new FieldChange(2, 5), result.Value.Fields["availableCopies"]);
    }

    [Fact]
    public void Reducing_total_copies_down_to_loaned_copies_is_allowed()
    {
        var book = NewBookWithLoans(totalCopies: 5, availableCopies: 2);

        var result = book.Update(null, null, 3, Later);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, book.TotalCopies);
        Assert.Equal(0, book.AvailableCopies);
    }

    [Fact]
    public void Reducing_total_copies_below_loaned_copies_is_rejected()
    {
        var book = NewBookWithLoans(totalCopies: 5, availableCopies: 2);

        var result = book.Update("Changed", null, 2, Later);

        Assert.Equal(BookErrors.CopiesBelowLoaned, result.Error);
        Assert.Equal(5, book.TotalCopies);
        Assert.Equal("Original Title", book.Title);
    }

    [Fact]
    public void Inactive_book_cannot_be_updated()
    {
        var book = NewBook(totalCopies: 1);
        book.Deactivate(Later);

        var result = book.Update("New Title", null, null, Later);

        Assert.Equal(BookErrors.Inactive, result.Error);
    }

    [Fact]
    public void Deactivate_is_idempotent()
    {
        var book = NewBook(totalCopies: 1);

        var first = book.Deactivate(Later);
        var second = book.Deactivate(Later.AddHours(1));

        Assert.True(first);
        Assert.False(second);
        Assert.False(book.IsActive);
        Assert.Equal(Later, book.UpdatedAt);
    }

    private static Book NewBook(int totalCopies) =>
        Book.Create("Original Title", "Original Author", SampleIsbn, totalCopies, CreatedAt).Value;

    private static Book NewBookWithLoans(int totalCopies, int availableCopies)
    {
        var book = NewBook(totalCopies);
        typeof(Book).GetProperty(nameof(Book.AvailableCopies))!.SetValue(book, availableCopies);
        return book;
    }
}
