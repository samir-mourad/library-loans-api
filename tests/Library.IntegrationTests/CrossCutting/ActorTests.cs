using System.Net;
using Library.IntegrationTests.Infrastructure;

namespace Library.IntegrationTests.CrossCutting;

public class ActorTests(ApiFactory factory)
{
    private const string HeaderName = "X-Actor-Id";

    [Fact]
    public async Task Valid_actor_is_accepted()
    {
        using var client = factory.CreateClient();

        var response = await client.SendGetAsync(TestEndpoints.Ok, HeaderName, "librarian-42");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Actor_longer_than_100_characters_is_rejected()
    {
        using var client = factory.CreateClient();

        var response = await client.SendGetAsync(TestEndpoints.Ok, HeaderName, new string('a', 101));
        var problem = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("actor.invalid", problem.GetProperty("code").GetString());
    }
}
