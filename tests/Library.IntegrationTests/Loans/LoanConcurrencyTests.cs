using System.Net;
using Library.Api.Features.Audit;
using Library.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Library.IntegrationTests.Loans;

/// <summary>
/// Two independent application hosts (separate DI containers and HTTP pipelines) share one PostgreSQL database,
/// simulating two replicas. All requests are released together by a start signal to maximize contention.
/// </summary>
public class LoanConcurrencyTests(ApiFactory factory)
{
    private const int Contenders = 20;

    [Fact]
    public async Task Last_copy_is_lent_exactly_once_across_replicas()
    {
        await using var replicaB = factory.WithWebHostBuilder(_ => { });
        using var clientA = factory.CreateClient();
        using var clientB = replicaB.CreateClient();
        var book = await clientA.CreateBookAsync(BookApi.NewCreateRequest(totalCopies: 1));
        var users = await CreateUsersAsync(clientA, Contenders);

        var responses = await RunConcurrentlyAsync(users.Select<Guid, Func<Task<HttpResponseMessage>>>((userId, index) =>
            () => (index % 2 == 0 ? clientA : clientB).RequestLoanAsync(book.Id, userId)));
        var rejectionCodes = await Task.WhenAll(responses
            .Where(response => response.StatusCode == HttpStatusCode.Conflict)
            .Select(response => response.ReadProblemCodeAsync()));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Equal(Contenders - 1, rejectionCodes.Length);
        Assert.All(rejectionCodes, code => Assert.Equal("book.unavailable", code));
        Assert.Equal(0, await factory.ReadAvailableCopiesAsync(book.Id));
        Assert.Equal(1, await factory.CountLoansAsync(book.Id));
        Assert.Equal(1, await CountLoanCreatedEventsAsync(book.Id));
    }

    [Fact]
    public async Task Each_of_several_copies_is_lent_exactly_once()
    {
        await using var replicaB = factory.WithWebHostBuilder(_ => { });
        using var clientA = factory.CreateClient();
        using var clientB = replicaB.CreateClient();
        var book = await clientA.CreateBookAsync(BookApi.NewCreateRequest(totalCopies: 3));
        var users = await CreateUsersAsync(clientA, Contenders);

        var responses = await RunConcurrentlyAsync(users.Select<Guid, Func<Task<HttpResponseMessage>>>((userId, index) =>
            () => (index % 2 == 0 ? clientA : clientB).RequestLoanAsync(book.Id, userId)));

        Assert.Equal(3, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(Contenders - 3, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(0, await factory.ReadAvailableCopiesAsync(book.Id));
        Assert.Equal(3, await factory.CountLoansAsync(book.Id));
    }

    [Fact]
    public async Task Concurrent_returns_restore_the_copy_only_once()
    {
        await using var replicaB = factory.WithWebHostBuilder(_ => { });
        using var clientA = factory.CreateClient();
        using var clientB = replicaB.CreateClient();
        var (book, loan) = await clientA.CreateBookWithLoanAsync(totalCopies: 1);

        var responses = await RunConcurrentlyAsync(
        [
            () => clientA.SendJsonAsync(HttpMethod.Post, LoanApi.ReturnPath(loan.Id), body: null),
            () => clientB.SendJsonAsync(HttpMethod.Post, LoanApi.ReturnPath(loan.Id), body: null)
        ]);

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, await factory.ReadAvailableCopiesAsync(book.Id));
    }

    [Fact]
    public async Task Same_user_cannot_get_two_active_loans_of_the_same_book_concurrently()
    {
        await using var replicaB = factory.WithWebHostBuilder(_ => { });
        using var clientA = factory.CreateClient();
        using var clientB = replicaB.CreateClient();
        var book = await clientA.CreateBookAsync(BookApi.NewCreateRequest(totalCopies: 2));
        var user = await clientA.CreateUserAsync();

        var responses = await RunConcurrentlyAsync(
        [
            () => clientA.RequestLoanAsync(book.Id, user.Id),
            () => clientB.RequestLoanAsync(book.Id, user.Id)
        ]);
        var rejected = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Equal("loan.duplicate_active", await rejected.ReadProblemCodeAsync());
        Assert.Equal(1, await factory.ReadAvailableCopiesAsync(book.Id));
        Assert.Equal(1, await factory.CountLoansAsync(book.Id));
    }

    private static async Task<HttpResponseMessage[]> RunConcurrentlyAsync(IEnumerable<Func<Task<HttpResponseMessage>>> requests)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = requests
            .Select(request => Task.Run(async () =>
            {
                await start.Task;
                return await request();
            }))
            .ToArray();

        start.SetResult();
        return await Task.WhenAll(running);
    }

    private static async Task<Guid[]> CreateUsersAsync(HttpClient client, int count)
    {
        var users = await Task.WhenAll(Enumerable.Range(0, count).Select(_ => client.CreateUserAsync()));
        return [.. users.Select(user => user.Id)];
    }

    private async Task<int> CountLoanCreatedEventsAsync(Guid bookId)
    {
        await using var context = factory.CreateDbContext();
        var loanIds = context.Loans.Where(loan => loan.BookId == bookId).Select(loan => loan.Id);

        return await context.AuditEvents.CountAsync(
            auditEvent => auditEvent.Action == AuditActions.LoanCreated && loanIds.Contains(auditEvent.EntityId),
            TestContext.Current.CancellationToken);
    }
}
