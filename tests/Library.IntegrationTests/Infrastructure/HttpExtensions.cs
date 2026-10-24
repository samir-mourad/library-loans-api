using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Library.IntegrationTests.Infrastructure;

internal static class HttpExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<HttpResponseMessage> SendGetAsync(this HttpClient client, string path, string headerName, string headerValue)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        request.Headers.TryAddWithoutValidation(headerName, headerValue);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static Task<HttpResponseMessage> SendGetAsync(this HttpClient client, string path) =>
        client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

    public static async Task<HttpResponseMessage> SendJsonAsync(
        this HttpClient client,
        HttpMethod method,
        string path,
        object? body,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        foreach (var (name, value) in headers ?? new Dictionary<string, string>())
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

    public static async Task<T> ReadAsAsync<T>(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(JsonOptions, TestContext.Current.CancellationToken)
        ?? throw new InvalidOperationException($"Response body could not be read as {typeof(T).Name}.");

    public static async Task<string?> ReadProblemCodeAsync(this HttpResponseMessage response)
    {
        var problem = await response.ReadJsonAsync();
        return problem.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}
