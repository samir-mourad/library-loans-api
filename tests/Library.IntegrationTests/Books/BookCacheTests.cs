using System.Net;
using System.Text.Json;
using Library.Api.Features.Books;
using Library.Api.Features.Loans;
using Library.IntegrationTests.Infrastructure;

namespace Library.IntegrationTests.Books;

public class BookCacheTests(ApiFactory factory)
{
    [Fact]
    public async Task First_read_stores_the_snapshot_with_a_ttl()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync();

        await client.SendGetAsync(BookApi.BookPath(book.Id));
        var timeToLive = await factory.Redis.KeyTimeToLiveAsync(BookCache.KeyFor(book.Id));

        Assert.NotNull(timeToLive);
        Assert.True(timeToLive > TimeSpan.Zero);
    }

    [Fact]
    public async Task Reads_are_served_from_the_cached_snapshot()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync(BookApi.NewCreateRequest(totalCopies: 5));
        var tampered = book with { Title = "Served from cache", AvailableCopies = 4 };
        await factory.Redis.StringSetAsync(BookCache.KeyFor(book.Id), JsonSerializer.Serialize(tampered, JsonSerializerOptions.Web), TimeSpan.FromMinutes(1));

        var cachedBook = await (await client.SendGetAsync(BookApi.BookPath(book.Id))).ReadAsAsync<BookResponse>();
        var cachedAvailability = await (await client.SendGetAsync(LoanApi.AvailabilityPath(book.Id))).ReadAsAsync<AvailabilityResponse>();

        Assert.Equal("Served from cache", cachedBook.Title);
        Assert.Equal(4, cachedAvailability.AvailableCopies);
        Assert.Equal(1, cachedAvailability.LoanedCopies);
    }

    [Fact]
    public async Task Lending_invalidates_the_snapshot()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync(BookApi.NewCreateRequest(totalCopies: 2));
        var user = await client.CreateUserAsync();
        await client.SendGetAsync(LoanApi.AvailabilityPath(book.Id));

        await client.CreateLoanAsync(book.Id, user.Id);
        var keyExists = await factory.Redis.KeyExistsAsync(BookCache.KeyFor(book.Id));
        var availability = await (await client.SendGetAsync(LoanApi.AvailabilityPath(book.Id))).ReadAsAsync<AvailabilityResponse>();

        Assert.False(keyExists);
        Assert.Equal(1, availability.AvailableCopies);
    }

    [Fact]
    public async Task Returning_invalidates_the_snapshot()
    {
        using var client = factory.CreateClient();
        var (book, loan) = await client.CreateBookWithLoanAsync(totalCopies: 1);
        await client.SendGetAsync(LoanApi.AvailabilityPath(book.Id));

        await client.SendJsonAsync(HttpMethod.Post, LoanApi.ReturnPath(loan.Id), body: null);
        var keyExists = await factory.Redis.KeyExistsAsync(BookCache.KeyFor(book.Id));
        var availability = await (await client.SendGetAsync(LoanApi.AvailabilityPath(book.Id))).ReadAsAsync<AvailabilityResponse>();

        Assert.False(keyExists);
        Assert.True(availability.IsAvailable);
    }

    [Fact]
    public async Task Cancelling_invalidates_the_snapshot()
    {
        using var client = factory.CreateClient();
        var (book, loan) = await client.CreateBookWithLoanAsync(totalCopies: 1);
        await client.SendGetAsync(LoanApi.AvailabilityPath(book.Id));

        await client.SendJsonAsync(HttpMethod.Post, LoanApi.CancelPath(loan.Id), body: null);

        Assert.False(await factory.Redis.KeyExistsAsync(BookCache.KeyFor(book.Id)));
    }

    [Fact]
    public async Task Updating_the_book_invalidates_the_snapshot()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync();
        await client.SendGetAsync(BookApi.BookPath(book.Id));

        await client.SendJsonAsync(HttpMethod.Patch, BookApi.BookPath(book.Id), new UpdateBookRequest { Title = "Second edition" });
        var keyExists = await factory.Redis.KeyExistsAsync(BookCache.KeyFor(book.Id));
        var current = await (await client.SendGetAsync(BookApi.BookPath(book.Id))).ReadAsAsync<BookResponse>();

        Assert.False(keyExists);
        Assert.Equal("Second edition", current.Title);
    }

    [Fact]
    public async Task Deactivating_the_book_invalidates_the_snapshot()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync();
        await client.SendGetAsync(BookApi.BookPath(book.Id));

        await client.SendJsonAsync(HttpMethod.Delete, BookApi.BookPath(book.Id), body: null);
        var keyExists = await factory.Redis.KeyExistsAsync(BookCache.KeyFor(book.Id));
        var current = await (await client.SendGetAsync(BookApi.BookPath(book.Id))).ReadAsAsync<BookResponse>();

        Assert.False(keyExists);
        Assert.False(current.IsActive);
    }

    [Fact]
    public async Task Idempotent_replay_keeps_the_snapshot()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync(BookApi.NewCreateRequest(totalCopies: 2));
        var user = await client.CreateUserAsync();
        var key = LoanApi.NewIdempotencyKey();
        await client.RequestLoanAsync(book.Id, user.Id, key);
        await client.SendGetAsync(LoanApi.AvailabilityPath(book.Id));

        await client.RequestLoanAsync(book.Id, user.Id, key);

        Assert.True(await factory.Redis.KeyExistsAsync(BookCache.KeyFor(book.Id)));
    }

    [Fact]
    public async Task Unknown_book_is_not_cached()
    {
        using var client = factory.CreateClient();
        var unknownId = Guid.CreateVersion7();

        var response = await client.SendGetAsync(BookApi.BookPath(unknownId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(await factory.Redis.KeyExistsAsync(BookCache.KeyFor(unknownId)));
    }

    [Fact]
    public async Task Api_keeps_working_when_redis_is_unreachable()
    {
        await using var app = factory.WithUnreachable(UnreachableDependencies.RedisKey, UnreachableDependencies.Redis);
        using var client = app.CreateClient();
        var book = await client.CreateBookAsync(BookApi.NewCreateRequest(totalCopies: 2));
        var user = await client.CreateUserAsync();

        var read = await client.SendGetAsync(BookApi.BookPath(book.Id));
        var loan = await client.RequestLoanAsync(book.Id, user.Id);
        var availability = await (await client.SendGetAsync(LoanApi.AvailabilityPath(book.Id))).ReadAsAsync<AvailabilityResponse>();

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.Created, loan.StatusCode);
        Assert.Equal(1, availability.AvailableCopies);
    }
}
