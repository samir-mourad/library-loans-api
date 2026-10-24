using Library.Domain.Common;

namespace Library.UnitTests.Common;

public class ErrorTests
{
    [Fact]
    public void Error_preserves_code_message_and_type()
    {
        var error = new Error("isbn.duplicate", "ISBN already registered.", ErrorType.Conflict);

        Assert.Equal("isbn.duplicate", error.Code);
        Assert.Equal("ISBN already registered.", error.Message);
        Assert.Equal(ErrorType.Conflict, error.Type);
    }

    [Theory]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Unprocessable)]
    public void Factory_methods_set_the_matching_type(ErrorType expected)
    {
        Func<string, string, Error> factory = expected switch
        {
            ErrorType.Validation => Error.Validation,
            ErrorType.NotFound => Error.NotFound,
            ErrorType.Conflict => Error.Conflict,
            _ => Error.Unprocessable
        };

        var error = factory("some.code", "Some message.");

        Assert.Equal(expected, error.Type);
    }
}
