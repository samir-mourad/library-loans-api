using System.Net;
using System.Text.Json;
using Library.Api.Features.Audit;
using Library.Api.Features.Books;
using Library.IntegrationTests.Infrastructure;

namespace Library.IntegrationTests.Books;

public class CreateBookTests(ApiFactory factory)
{
    [Fact]
    public async Task Creating_a_book_returns_201_and_records_an_audit_event()
    {
        using var client = factory.CreateClient();
        var request = BookApi.NewCreateRequest(totalCopies: 4);
        var headers = new Dictionary<string, string> { ["X-Actor-Id"] = "librarian-7", ["X-Correlation-Id"] = "create-book-1" };

        var response = await client.SendJsonAsync(HttpMethod.Post, BookApi.BooksPath, request, headers);
        var book = await response.ReadAsAsync<BookResponse>();
        var auditEvent = Assert.Single(await factory.ReadAuditEventsAsync(book.Id));
        using var data = JsonDocument.Parse(auditEvent.Data);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(BookApi.BookPath(book.Id), response.Headers.Location?.OriginalString);
        Assert.Equal(4, book.TotalCopies);
        Assert.Equal(4, book.AvailableCopies);
        Assert.True(book.IsActive);
        Assert.Equal(AuditActions.BookCreated, auditEvent.Action);
        Assert.Equal(AuditEntityTypes.Book, auditEvent.EntityType);
        Assert.Equal("librarian-7", auditEvent.Actor);
        Assert.Equal("create-book-1", auditEvent.CorrelationId);
        Assert.Equal(request.Title, data.RootElement.GetProperty("title").GetString());
        Assert.Equal(request.Isbn, data.RootElement.GetProperty("isbn").GetString());
        Assert.Equal(4, data.RootElement.GetProperty("totalCopies").GetInt32());
    }

    [Fact]
    public async Task Creating_a_book_with_an_existing_isbn_returns_409()
    {
        using var client = factory.CreateClient();
        var isbn = TestIsbn.NewIsbn13();
        await client.CreateBookAsync(BookApi.NewCreateRequest(isbn: isbn));

        var response = await client.SendJsonAsync(HttpMethod.Post, BookApi.BooksPath, BookApi.NewCreateRequest(isbn: isbn));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("isbn.duplicate", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Isbn10_equivalent_of_an_existing_isbn13_is_a_duplicate()
    {
        using var client = factory.CreateClient();
        var (isbn10, isbn13) = TestIsbn.NewPair();
        await client.CreateBookAsync(BookApi.NewCreateRequest(isbn: isbn13));

        var response = await client.SendJsonAsync(HttpMethod.Post, BookApi.BooksPath, BookApi.NewCreateRequest(isbn: isbn10));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("isbn.duplicate", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Invalid_payload_returns_400_validation_problem()
    {
        using var client = factory.CreateClient();
        var request = new CreateBookRequest { Title = "", Author = "Author", Isbn = TestIsbn.NewIsbn13(), TotalCopies = 0 };

        var response = await client.SendJsonAsync(HttpMethod.Post, BookApi.BooksPath, request);
        var problem = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation.failed", problem.GetProperty("code").GetString());
        Assert.True(problem.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Isbn_with_wrong_check_digit_returns_400()
    {
        using var client = factory.CreateClient();
        var request = BookApi.NewCreateRequest(isbn: "9780306406158");

        var response = await client.SendJsonAsync(HttpMethod.Post, BookApi.BooksPath, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("isbn.invalid", await response.ReadProblemCodeAsync());
    }
}
