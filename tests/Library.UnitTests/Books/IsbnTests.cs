using Library.Domain.Books;

namespace Library.UnitTests.Books;

public class IsbnTests
{
    [Theory]
    [InlineData("9780306406157", "9780306406157")]
    [InlineData("978-0-306-40615-7", "9780306406157")]
    [InlineData("978 3 16 148410 0", "9783161484100")]
    [InlineData("0-306-40615-2", "9780306406157")]
    [InlineData("0306406152", "9780306406157")]
    [InlineData("0-8044-2957-X", "9780804429573")]
    [InlineData("0-8044-2957-x", "9780804429573")]
    public void Valid_isbn_is_normalized_to_isbn13(string raw, string expected)
    {
        var result = Isbn.Create(raw);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.Value);
    }

    [Theory]
    [InlineData("9780306406158")]
    [InlineData("0-306-40615-3")]
    [InlineData("12345")]
    [InlineData("97803064061570")]
    [InlineData("97803064061AB")]
    [InlineData("X306406152")]
    [InlineData("")]
    [InlineData(null)]
    public void Invalid_isbn_is_rejected(string? raw)
    {
        var result = Isbn.Create(raw);

        Assert.True(result.IsFailure);
        Assert.Equal(BookErrors.InvalidIsbn, result.Error);
    }

    [Fact]
    public void Isbn10_and_its_isbn13_equivalent_are_equal()
    {
        var isbn10 = Isbn.Create("0-306-40615-2").Value;
        var isbn13 = Isbn.Create("978-0-306-40615-7").Value;

        Assert.Equal(isbn13, isbn10);
    }
}
