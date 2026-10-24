using System.Net;
using Library.Api.Features.Audit;
using Library.Api.Features.Books;
using Library.Api.Infrastructure.Pagination;
using Library.IntegrationTests.Infrastructure;

namespace Library.IntegrationTests.Audit;

public class AuditEventsQueryTests(ApiFactory factory)
{
    [Fact]
    public async Task Events_of_an_entity_are_returned_newest_first()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync();
        await client.SendJsonAsync(HttpMethod.Patch, BookApi.BookPath(book.Id), new UpdateBookRequest { Title = "Second edition" });
        await client.SendJsonAsync(HttpMethod.Delete, BookApi.BookPath(book.Id), body: null);

        var response = await client.SendGetAsync($"/audit-events?entityType={AuditEntityTypes.Book}&entityId={book.Id}");
        var page = await response.ReadAsAsync<PagedResponse<AuditEventResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(
            [AuditActions.BookDeactivated, AuditActions.BookUpdated, AuditActions.BookCreated],
            page.Items.Select(auditEvent => auditEvent.Action));
        Assert.Equal("Second edition", page.Items[1].Data.GetProperty("changes").GetProperty("title").GetProperty("to").GetString());
    }

    [Fact]
    public async Task Events_can_be_filtered_by_action()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync();
        await client.SendJsonAsync(HttpMethod.Delete, BookApi.BookPath(book.Id), body: null);

        var response = await client.SendGetAsync($"/audit-events?entityId={book.Id}&action={AuditActions.BookDeactivated}");
        var page = await response.ReadAsAsync<PagedResponse<AuditEventResponse>>();

        Assert.Equal(AuditActions.BookDeactivated, Assert.Single(page.Items).Action);
    }
}
