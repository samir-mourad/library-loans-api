using System.Net;
using Library.Domain.Common;
using Library.IntegrationTests.Infrastructure;

namespace Library.IntegrationTests.CrossCutting;

public class ErrorHandlingTests(ApiFactory factory)
{
    private const string ProblemContentType = "application/problem+json";

    [Fact]
    public async Task Unhandled_exception_returns_generic_500_problem_without_details()
    {
        using var client = factory.CreateClient();

        var response = await client.SendGetAsync(TestEndpoints.Throw);
        var rawBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var problem = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(ProblemContentType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("internal.error", problem.GetProperty("code").GetString());
        Assert.Equal(Assert.Single(response.Headers.GetValues("X-Correlation-Id")), problem.GetProperty("correlationId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        Assert.DoesNotContain(TestEndpoints.SensitiveMessage, rawBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ErrorType.Validation, HttpStatusCode.BadRequest)]
    [InlineData(ErrorType.NotFound, HttpStatusCode.NotFound)]
    [InlineData(ErrorType.Conflict, HttpStatusCode.Conflict)]
    [InlineData(ErrorType.Unprocessable, HttpStatusCode.UnprocessableEntity)]
    public async Task Domain_error_is_mapped_to_its_status_and_code(ErrorType type, HttpStatusCode expectedStatus)
    {
        using var client = factory.CreateClient();

        var response = await client.SendGetAsync(TestEndpoints.ErrorsPrefix + type);
        var problem = await response.ReadJsonAsync();

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(ProblemContentType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(TestEndpoints.ErrorCodeFor(type), problem.GetProperty("code").GetString());
        Assert.True(problem.TryGetProperty("correlationId", out _));
    }

    [Fact]
    public async Task Unknown_route_returns_404_problem()
    {
        using var client = factory.CreateClient();

        var response = await client.SendGetAsync("/does-not-exist");
        var problem = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ProblemContentType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("route.not_found", problem.GetProperty("code").GetString());
    }
}
