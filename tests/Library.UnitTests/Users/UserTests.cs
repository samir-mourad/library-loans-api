using Library.Domain.Users;

namespace Library.UnitTests.Users;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly Email SampleEmail = Email.Create("ana@example.com").Value;

    [Fact]
    public void Create_trims_the_name()
    {
        var result = User.Create("  Ana Silva ", SampleEmail, Now);

        var user = result.Value;
        Assert.Equal("Ana Silva", user.Name);
        Assert.Equal(SampleEmail, user.Email);
        Assert.Equal(Now, user.CreatedAt);
        Assert.NotEqual(Guid.Empty, user.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_name(string name)
    {
        var result = User.Create(name, SampleEmail, Now);

        Assert.Equal(UserErrors.InvalidName, result.Error);
    }

    [Fact]
    public void Create_rejects_name_longer_than_maximum()
    {
        var result = User.Create(new string('a', User.NameMaxLength + 1), SampleEmail, Now);

        Assert.Equal(UserErrors.InvalidName, result.Error);
    }
}
