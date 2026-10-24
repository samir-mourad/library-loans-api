using System.Net.Http.Json;
using System.Text.Json;

namespace Library.IntegrationTests.Infrastructure;

internal static class HttpExtensions
{
    public static async Task<HttpResponseMessage> SendGetAsync(this HttpClient client, string path, string headerName, string headerValue)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        request.Headers.TryAddWithoutValidation(headerName, headerValue);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static Task<HttpResponseMessage> SendGetAsync(this HttpClient client, string path) =>
        client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

    public static Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
}
