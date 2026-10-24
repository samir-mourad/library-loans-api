using System.Net;
using Library.Api.Features.Audit;
using Library.Api.Features.Users;
using Library.IntegrationTests.Infrastructure;

namespace Library.IntegrationTests.Users;

public class UserTests(ApiFactory factory)
{
    [Fact]
    public async Task Registering_a_user_returns_201_with_normalized_email()
    {
        using var client = factory.CreateClient();
        var email = UserApi.NewEmail();
        var request = UserApi.NewCreateRequest(email: $"  {email.ToUpperInvariant()} ", name: " Ana Silva ");

        var response = await client.SendJsonAsync(HttpMethod.Post, UserApi.UsersPath, request);
        var user = await response.ReadAsAsync<UserResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(UserApi.UserPath(user.Id), response.Headers.Location?.OriginalString);
        Assert.Equal(email, user.Email);
        Assert.Equal("Ana Silva", user.Name);
    }

    [Fact]
    public async Task Registration_is_audited_without_personal_data()
    {
        using var client = factory.CreateClient();
        var request = UserApi.NewCreateRequest(name: "Maria Personal Data");
        var headers = new Dictionary<string, string> { ["X-Actor-Id"] = "front-desk", ["X-Correlation-Id"] = "register-user-1" };

        var response = await client.SendJsonAsync(HttpMethod.Post, UserApi.UsersPath, request, headers);
        var user = await response.ReadAsAsync<UserResponse>();
        var auditEvent = Assert.Single(await factory.ReadAuditEventsAsync(user.Id));

        Assert.Equal(AuditActions.UserCreated, auditEvent.Action);
        Assert.Equal(AuditEntityTypes.User, auditEvent.EntityType);
        Assert.Equal("front-desk", auditEvent.Actor);
        Assert.Equal("register-user-1", auditEvent.CorrelationId);
        Assert.DoesNotContain("Maria", auditEvent.Data, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(user.Email, auditEvent.Data, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Email_already_registered_with_different_case_returns_409()
    {
        using var client = factory.CreateClient();
        var email = UserApi.NewEmail();
        await client.CreateUserAsync(UserApi.NewCreateRequest(email: email));

        var response = await client.SendJsonAsync(HttpMethod.Post, UserApi.UsersPath, UserApi.NewCreateRequest(email: email.ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("user.email_duplicate", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Missing_fields_return_400_validation_problem()
    {
        using var client = factory.CreateClient();

        var response = await client.SendJsonAsync(HttpMethod.Post, UserApi.UsersPath, new CreateUserRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation.failed", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Malformed_email_returns_400()
    {
        using var client = factory.CreateClient();

        var response = await client.SendJsonAsync(HttpMethod.Post, UserApi.UsersPath, UserApi.NewCreateRequest(email: "not-an-email"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("user.email_invalid", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Existing_user_is_returned()
    {
        using var client = factory.CreateClient();
        var created = await client.CreateUserAsync();

        var response = await client.SendGetAsync(UserApi.UserPath(created.Id));
        var user = await response.ReadAsAsync<UserResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created, user);
    }

    [Fact]
    public async Task Unknown_user_returns_404()
    {
        using var client = factory.CreateClient();

        var response = await client.SendGetAsync(UserApi.UserPath(Guid.CreateVersion7()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("user.not_found", await response.ReadProblemCodeAsync());
    }
}
