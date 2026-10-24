using System.Net;
using Library.Api.Features.Audit;
using Library.Api.Features.Books;
using Microsoft.EntityFrameworkCore;

namespace Library.IntegrationTests.Infrastructure;

/// <summary>
/// Arrange and assert helpers for catalog tests.
/// </summary>
internal static class BookApi
{
    public const string BooksPath = "/books";

    public static string BookPath(Guid id) => $"{BooksPath}/{id}";

    public static CreateBookRequest NewCreateRequest(int totalCopies = 3, string? isbn = null, string? title = null) => new()
    {
        Title = title ?? $"Book {Guid.NewGuid():N}",
        Author = "Test Author",
        Isbn = isbn ?? TestIsbn.NewIsbn13(),
        TotalCopies = totalCopies
    };

    public static async Task<BookResponse> CreateBookAsync(this HttpClient client, CreateBookRequest? request = null)
    {
        var response = await client.SendJsonAsync(HttpMethod.Post, BooksPath, request ?? NewCreateRequest());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsAsync<BookResponse>();
    }

    public static async Task<IReadOnlyList<AuditEvent>> ReadAuditEventsAsync(this ApiFactory factory, Guid entityId)
    {
        await using var context = factory.CreateDbContext();

        return await context.AuditEvents
            .Where(auditEvent => auditEvent.EntityId == entityId)
            .OrderBy(auditEvent => auditEvent.OccurredAt)
            .ThenBy(auditEvent => auditEvent.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
