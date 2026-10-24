using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Library.IntegrationTests;

public class HealthLiveTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Live_endpoint_returns_200_without_dependencies()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
