using System.Net;
using Library.Api.Features.Audit;
using Library.Api.Features.Books;
using Library.IntegrationTests.Infrastructure;

namespace Library.IntegrationTests.Books;

public class DeactivateBookTests(ApiFactory factory)
{
    [Fact]
    public async Task Deactivating_a_book_keeps_it_readable_and_records_an_event()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync();

        var response = await client.SendJsonAsync(HttpMethod.Delete, BookApi.BookPath(book.Id), body: null);
        var current = await (await client.SendGetAsync(BookApi.BookPath(book.Id))).ReadAsAsync<BookResponse>();
        var auditEvents = await factory.ReadAuditEventsAsync(book.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(current.IsActive);
        Assert.Equal(AuditActions.BookDeactivated, auditEvents[^1].Action);
    }

    [Fact]
    public async Task Deactivating_twice_is_idempotent_and_records_a_single_event()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync();
        await client.SendJsonAsync(HttpMethod.Delete, BookApi.BookPath(book.Id), body: null);

        var response = await client.SendJsonAsync(HttpMethod.Delete, BookApi.BookPath(book.Id), body: null);
        var auditEvents = await factory.ReadAuditEventsAsync(book.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single(auditEvents, auditEvent => auditEvent.Action == AuditActions.BookDeactivated);
    }

    [Fact]
    public async Task Deactivating_an_unknown_book_returns_404()
    {
        using var client = factory.CreateClient();

        var response = await client.SendJsonAsync(HttpMethod.Delete, BookApi.BookPath(Guid.CreateVersion7()), body: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("book.not_found", await response.ReadProblemCodeAsync());
    }
}
