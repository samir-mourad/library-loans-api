using System.Net;
using Library.Api.Features.Audit;
using Library.Api.Features.Loans;
using Library.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Library.IntegrationTests.Loans;

public class IdempotentLoanTests(ApiFactory factory)
{
    private const string ReplayedHeader = "Idempotent-Replayed";

    [Fact]
    public async Task Missing_idempotency_key_returns_400()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync();
        var user = await client.CreateUserAsync();

        var response = await client.SendJsonAsync(HttpMethod.Post, LoanApi.LoansPath, new CreateLoanRequest { BookId = book.Id, UserId = user.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("idempotency.key_missing", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Malformed_idempotency_key_returns_400()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync();
        var user = await client.CreateUserAsync();

        var response = await client.RequestLoanAsync(book.Id, user.Id, idempotencyKey: "has spaces");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("idempotency.key_invalid", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Repeating_a_request_with_the_same_key_creates_a_single_loan()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync(BookApi.NewCreateRequest(totalCopies: 3));
        var user = await client.CreateUserAsync();
        var key = LoanApi.NewIdempotencyKey();

        var first = await client.RequestLoanAsync(book.Id, user.Id, key);
        var second = await client.RequestLoanAsync(book.Id, user.Id, key);
        var firstLoan = await first.ReadAsAsync<LoanResponse>();
        var secondLoan = await second.ReadAsAsync<LoanResponse>();

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.False(first.Headers.Contains(ReplayedHeader));
        Assert.Equal("true", Assert.Single(second.Headers.GetValues(ReplayedHeader)));
        Assert.Equal(firstLoan, secondLoan);
        Assert.Equal(first.Headers.Location, second.Headers.Location);
        Assert.Equal(2, await factory.ReadAvailableCopiesAsync(book.Id));
        Assert.Equal(1, await factory.CountLoansAsync(book.Id));
        Assert.Single(await factory.ReadAuditEventsAsync(firstLoan.Id), auditEvent => auditEvent.Action == AuditActions.LoanCreated);
    }

    [Fact]
    public async Task Concurrent_requests_with_the_same_key_across_replicas_create_a_single_loan()
    {
        await using var replicaB = factory.WithWebHostBuilder(_ => { });
        using var clientA = factory.CreateClient();
        using var clientB = replicaB.CreateClient();
        var book = await clientA.CreateBookAsync(BookApi.NewCreateRequest(totalCopies: 5));
        var user = await clientA.CreateUserAsync();
        var key = LoanApi.NewIdempotencyKey();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var attempts = Enumerable.Range(0, 10)
            .Select(index => Task.Run(async () =>
            {
                await start.Task;
                return await (index % 2 == 0 ? clientA : clientB).RequestLoanAsync(book.Id, user.Id, key);
            }))
            .ToArray();
        start.SetResult();
        var responses = await Task.WhenAll(attempts);
        var loanIds = await Task.WhenAll(responses.Select(async response => (await response.ReadAsAsync<LoanResponse>()).Id));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        Assert.Single(loanIds.Distinct());
        Assert.Equal(9, responses.Count(response => response.Headers.Contains(ReplayedHeader)));
        Assert.Equal(4, await factory.ReadAvailableCopiesAsync(book.Id));
        Assert.Equal(1, await factory.CountLoansAsync(book.Id));
    }

    [Fact]
    public async Task Same_key_with_a_different_request_returns_422()
    {
        using var client = factory.CreateClient();
        var firstBook = await client.CreateBookAsync();
        var secondBook = await client.CreateBookAsync();
        var user = await client.CreateUserAsync();
        var key = LoanApi.NewIdempotencyKey();
        await client.RequestLoanAsync(firstBook.Id, user.Id, key);

        var response = await client.RequestLoanAsync(secondBook.Id, user.Id, key);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("idempotency.key_reused", await response.ReadProblemCodeAsync());
        Assert.Equal(secondBook.TotalCopies, await factory.ReadAvailableCopiesAsync(secondBook.Id));
    }

    [Fact]
    public async Task Stored_rejection_is_replayed_even_after_a_copy_is_freed()
    {
        using var client = factory.CreateClient();
        var (book, existingLoan) = await client.CreateBookWithLoanAsync(totalCopies: 1);
        var user = await client.CreateUserAsync();
        var key = LoanApi.NewIdempotencyKey();

        var rejected = await client.RequestLoanAsync(book.Id, user.Id, key);
        await client.SendJsonAsync(HttpMethod.Post, LoanApi.ReturnPath(existingLoan.Id), body: null);
        var replayed = await client.RequestLoanAsync(book.Id, user.Id, key);
        var newAttempt = await client.RequestLoanAsync(book.Id, user.Id);

        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, replayed.StatusCode);
        Assert.Equal("book.unavailable", await replayed.ReadProblemCodeAsync());
        Assert.True(replayed.Headers.Contains(ReplayedHeader));
        Assert.Equal(HttpStatusCode.Created, newAttempt.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_loan_rejection_is_stored_without_partial_effects()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync(BookApi.NewCreateRequest(totalCopies: 3));
        var user = await client.CreateUserAsync();
        await client.CreateLoanAsync(book.Id, user.Id);
        var key = LoanApi.NewIdempotencyKey();

        var rejected = await client.RequestLoanAsync(book.Id, user.Id, key);
        var replayed = await client.RequestLoanAsync(book.Id, user.Id, key);

        Assert.Equal("loan.duplicate_active", await rejected.ReadProblemCodeAsync());
        Assert.Equal("loan.duplicate_active", await replayed.ReadProblemCodeAsync());
        Assert.True(replayed.Headers.Contains(ReplayedHeader));
        Assert.Equal(2, await factory.ReadAvailableCopiesAsync(book.Id));
        Assert.Equal(1, await factory.CountLoansAsync(book.Id));
        Assert.Equal(1, await CountStoredKeysAsync(key));
    }

    [Fact]
    public async Task Different_keys_do_not_bypass_business_rules()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync(BookApi.NewCreateRequest(totalCopies: 2));
        var user = await client.CreateUserAsync();
        await client.RequestLoanAsync(book.Id, user.Id);

        var response = await client.RequestLoanAsync(book.Id, user.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("loan.duplicate_active", await response.ReadProblemCodeAsync());
    }

    private async Task<int> CountStoredKeysAsync(string key)
    {
        await using var context = factory.CreateDbContext();

        return await context.IdempotencyRecords.CountAsync(
            record => record.Key == key && record.StatusCode != null,
            TestContext.Current.CancellationToken);
    }
}
