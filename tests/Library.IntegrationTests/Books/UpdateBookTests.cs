using System.Net;
using System.Text.Json;
using Library.Api.Features.Audit;
using Library.Api.Features.Books;
using Library.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Library.IntegrationTests.Books;

public class UpdateBookTests(ApiFactory factory)
{
    [Fact]
    public async Task Updating_the_title_records_only_the_title_diff()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync();

        var response = await client.SendJsonAsync(HttpMethod.Patch, BookApi.BookPath(book.Id), new UpdateBookRequest { Title = "Refactoring" });
        var updated = await response.ReadAsAsync<BookResponse>();
        var auditEvent = (await factory.ReadAuditEventsAsync(book.Id))[^1];
        using var data = JsonDocument.Parse(auditEvent.Data);
        var changes = data.RootElement.GetProperty("changes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Refactoring", updated.Title);
        Assert.Equal(AuditActions.BookUpdated, auditEvent.Action);
        Assert.Equal(["title"], changes.EnumerateObject().Select(property => property.Name));
        Assert.Equal(book.Title, changes.GetProperty("title").GetProperty("from").GetString());
        Assert.Equal("Refactoring", changes.GetProperty("title").GetProperty("to").GetString());
    }

    [Fact]
    public async Task Increasing_copies_increases_availability()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync(BookApi.NewCreateRequest(totalCopies: 2));

        var response = await client.SendJsonAsync(HttpMethod.Patch, BookApi.BookPath(book.Id), new UpdateBookRequest { TotalCopies = 5 });
        var updated = await response.ReadAsAsync<BookResponse>();

        Assert.Equal(5, updated.TotalCopies);
        Assert.Equal(5, updated.AvailableCopies);
    }

    [Fact]
    public async Task Reducing_copies_below_loaned_copies_returns_409_and_changes_nothing()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync(BookApi.NewCreateRequest(totalCopies: 3));
        await SimulateLoanedCopiesAsync(book.Id, availableCopies: 1);

        var response = await client.SendJsonAsync(HttpMethod.Patch, BookApi.BookPath(book.Id), new UpdateBookRequest { TotalCopies = 1 });
        var current = await (await client.SendGetAsync(BookApi.BookPath(book.Id))).ReadAsAsync<BookResponse>();
        var auditEvents = await factory.ReadAuditEventsAsync(book.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("book.copies_below_loaned", await response.ReadProblemCodeAsync());
        Assert.Equal(3, current.TotalCopies);
        Assert.Equal(1, current.AvailableCopies);
        Assert.DoesNotContain(auditEvents, auditEvent => auditEvent.Action == AuditActions.BookUpdated);
    }

    [Fact]
    public async Task Request_without_changes_records_no_event()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync();

        var response = await client.SendJsonAsync(HttpMethod.Patch, BookApi.BookPath(book.Id), new UpdateBookRequest { Title = book.Title });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(await factory.ReadAuditEventsAsync(book.Id));
    }

    [Fact]
    public async Task Updating_an_inactive_book_returns_409()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync();
        await client.SendJsonAsync(HttpMethod.Delete, BookApi.BookPath(book.Id), body: null);

        var response = await client.SendJsonAsync(HttpMethod.Patch, BookApi.BookPath(book.Id), new UpdateBookRequest { Title = "Too late" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("book.inactive", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Updating_an_unknown_book_returns_404()
    {
        using var client = factory.CreateClient();

        var response = await client.SendJsonAsync(HttpMethod.Patch, BookApi.BookPath(Guid.CreateVersion7()), new UpdateBookRequest { Title = "Ghost" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task SimulateLoanedCopiesAsync(Guid bookId, int availableCopies)
    {
        await using var context = factory.CreateDbContext();
        await context.Books
            .Where(book => book.Id == bookId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(book => book.AvailableCopies, availableCopies), TestContext.Current.CancellationToken);
    }
}
