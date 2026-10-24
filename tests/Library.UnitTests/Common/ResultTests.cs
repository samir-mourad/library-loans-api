using Library.Domain.Common;

namespace Library.UnitTests.Common;

public class ResultTests
{
    private static readonly Error SampleError = Error.Conflict("book.unavailable", "No copies available.");

    [Fact]
    public void Success_result_has_no_error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_result_carries_the_error()
    {
        var result = Result.Failure(SampleError);

        Assert.True(result.IsFailure);
        Assert.Same(SampleError, result.Error);
    }

    [Fact]
    public void Generic_success_result_exposes_the_value()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Generic_failure_result_does_not_expose_a_value()
    {
        Result<int> result = SampleError;

        Assert.True(result.IsFailure);
        Assert.Same(SampleError, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Failure_requires_an_error()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));
    }
}
