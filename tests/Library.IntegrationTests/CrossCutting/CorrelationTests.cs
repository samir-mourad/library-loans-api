using Library.IntegrationTests.Infrastructure;

namespace Library.IntegrationTests.CrossCutting;

public class CorrelationTests(ApiFactory factory)
{
    private const string HeaderName = "X-Correlation-Id";
    private const string GeneratedIdPattern = "^[0-9a-f]{32}$";

    [Fact]
    public async Task Valid_correlation_id_is_echoed_in_the_response()
    {
        using var client = factory.CreateClient();

        var response = await client.SendGetAsync(TestEndpoints.Ok, HeaderName, "order-123:abc");

        Assert.Equal("order-123:abc", ResponseCorrelationId(response));
    }

    [Fact]
    public async Task Missing_correlation_id_is_generated()
    {
        using var client = factory.CreateClient();

        var response = await client.SendGetAsync(TestEndpoints.Ok);

        Assert.Matches(GeneratedIdPattern, ResponseCorrelationId(response));
    }

    public static TheoryData<string> InvalidCorrelationIds => ["has spaces", "semi;colon", new string('a', 129)];

    [Theory]
    [MemberData(nameof(InvalidCorrelationIds))]
    public async Task Invalid_correlation_id_is_replaced_by_a_generated_one(string invalidValue)
    {
        using var client = factory.CreateClient();

        var response = await client.SendGetAsync(TestEndpoints.Ok, HeaderName, invalidValue);

        Assert.Matches(GeneratedIdPattern, ResponseCorrelationId(response));
    }

    private static string ResponseCorrelationId(HttpResponseMessage response) =>
        Assert.Single(response.Headers.GetValues(HeaderName));
}
