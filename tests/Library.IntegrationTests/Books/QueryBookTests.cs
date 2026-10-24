using System.Net;
using Library.Api.Features.Books;
using Library.Api.Infrastructure.Pagination;
using Library.IntegrationTests.Infrastructure;

namespace Library.IntegrationTests.Books;

public class QueryBookTests(ApiFactory factory)
{
    [Fact]
    public async Task Existing_book_is_returned()
    {
        using var client = factory.CreateClient();
        var created = await client.CreateBookAsync();

        var response = await client.SendGetAsync(BookApi.BookPath(created.Id));
        var book = await response.ReadAsAsync<BookResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created, book);
    }

    [Fact]
    public async Task Unknown_book_returns_404()
    {
        using var client = factory.CreateClient();

        var response = await client.SendGetAsync(BookApi.BookPath(Guid.CreateVersion7()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("book.not_found", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Search_filters_by_title_case_insensitively()
    {
        using var client = factory.CreateClient();
        var marker = Guid.NewGuid().ToString("N");
        var match = await client.CreateBookAsync(BookApi.NewCreateRequest(title: $"Domain Driven {marker}"));
        await client.CreateBookAsync();

        var response = await client.SendGetAsync($"{BookApi.BooksPath}?search=DRIVEN%20{marker.ToUpperInvariant()}");
        var page = await response.ReadAsAsync<PagedResponse<BookResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(match.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task Isbn_filter_accepts_any_isbn_format()
    {
        using var client = factory.CreateClient();
        var (isbn10, isbn13) = TestIsbn.NewPair();
        var created = await client.CreateBookAsync(BookApi.NewCreateRequest(isbn: isbn13));

        var response = await client.SendGetAsync($"{BookApi.BooksPath}?isbn={isbn10}");
        var page = await response.ReadAsAsync<PagedResponse<BookResponse>>();

        Assert.Equal(created.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task Inactive_books_are_hidden_unless_requested()
    {
        using var client = factory.CreateClient();
        var marker = Guid.NewGuid().ToString("N");
        var book = await client.CreateBookAsync(BookApi.NewCreateRequest(title: $"Retired {marker}"));
        await client.SendJsonAsync(HttpMethod.Delete, BookApi.BookPath(book.Id), body: null);

        var hidden = await (await client.SendGetAsync($"{BookApi.BooksPath}?search={marker}")).ReadAsAsync<PagedResponse<BookResponse>>();
        var visible = await (await client.SendGetAsync($"{BookApi.BooksPath}?search={marker}&includeInactive=true")).ReadAsAsync<PagedResponse<BookResponse>>();

        Assert.Empty(hidden.Items);
        Assert.False(Assert.Single(visible.Items).IsActive);
    }

    [Fact]
    public async Task Page_size_above_maximum_returns_400()
    {
        using var client = factory.CreateClient();

        var response = await client.SendGetAsync($"{BookApi.BooksPath}?pageSize={Paging.MaxPageSize + 1}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation.failed", await response.ReadProblemCodeAsync());
    }
}
