using System.Net;
using Library.Api.Features.Users;

namespace Library.IntegrationTests.Infrastructure;

/// <summary>
/// Arrange helpers for user tests. E-mails are unique per call so tests can share the database in parallel.
/// </summary>
internal static class UserApi
{
    public const string UsersPath = "/users";

    public static string UserPath(Guid id) => $"{UsersPath}/{id}";

    public static string NewEmail() => $"reader-{Guid.NewGuid():N}@example.com";

    public static CreateUserRequest NewCreateRequest(string? email = null, string name = "Test Reader") => new()
    {
        Name = name,
        Email = email ?? NewEmail()
    };

    public static async Task<UserResponse> CreateUserAsync(this HttpClient client, CreateUserRequest? request = null)
    {
        var response = await client.SendJsonAsync(HttpMethod.Post, UsersPath, request ?? NewCreateRequest());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsAsync<UserResponse>();
    }
}
