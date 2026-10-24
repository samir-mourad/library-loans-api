using Library.Domain.Users;

namespace Library.UnitTests.Users;

public class EmailTests
{
    [Theory]
    [InlineData("ana@example.com", "ana@example.com")]
    [InlineData("  Ana.Silva@Example.COM ", "ana.silva@example.com")]
    [InlineData("leitor+biblioteca@mail.example.org", "leitor+biblioteca@mail.example.org")]
    public void Valid_email_is_normalized(string raw, string expected)
    {
        var result = Email.Create(raw);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.Value);
    }

    [Theory]
    [InlineData("ana.example.com")]
    [InlineData("ana@")]
    [InlineData("@example.com")]
    [InlineData("ana@example")]
    [InlineData("ana@example.")]
    [InlineData("ana@.com")]
    [InlineData("ana silva@example.com")]
    [InlineData("ana@@example.com")]
    [InlineData("")]
    [InlineData(null)]
    public void Invalid_email_is_rejected(string? raw)
    {
        var result = Email.Create(raw);

        Assert.Equal(UserErrors.InvalidEmail, result.Error);
    }

    [Fact]
    public void Email_longer_than_maximum_is_rejected()
    {
        var raw = new string('a', Email.MaxLength - "@example.com".Length + 1) + "@example.com";

        var result = Email.Create(raw);

        Assert.Equal(UserErrors.InvalidEmail, result.Error);
    }

    [Fact]
    public void Emails_differing_only_in_case_are_equal()
    {
        var lower = Email.Create("ana@example.com").Value;
        var mixed = Email.Create("ANA@Example.com").Value;

        Assert.Equal(lower, mixed);
    }
}
