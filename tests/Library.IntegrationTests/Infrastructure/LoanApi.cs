using System.Net;
using Library.Api.Features.Books;
using Library.Api.Features.Loans;
using Microsoft.EntityFrameworkCore;

namespace Library.IntegrationTests.Infrastructure;

/// <summary>
/// Arrange and assert helpers for loan tests.
/// </summary>
internal static class LoanApi
{
    public const string LoansPath = "/loans";

    public static string LoanPath(Guid id) => $"{LoansPath}/{id}";

    public static string ReturnPath(Guid id) => $"{LoanPath(id)}/return";

    public static string CancelPath(Guid id) => $"{LoanPath(id)}/cancel";

    public static string AvailabilityPath(Guid bookId) => $"{BookApi.BookPath(bookId)}/availability";

    public static string BookHistoryPath(Guid bookId) => $"{BookApi.BookPath(bookId)}/history";

    public static string UserLoansPath(Guid userId) => $"{UserApi.UserPath(userId)}/loans";

    public const string IdempotencyKeyHeader = "Idempotency-Key";

    public static string NewIdempotencyKey() => Guid.NewGuid().ToString();

    public static Task<HttpResponseMessage> RequestLoanAsync(this HttpClient client, Guid bookId, Guid userId, string? idempotencyKey = null) =>
        client.SendJsonAsync(
            HttpMethod.Post,
            LoansPath,
            new CreateLoanRequest { BookId = bookId, UserId = userId },
            new Dictionary<string, string> { [IdempotencyKeyHeader] = idempotencyKey ?? NewIdempotencyKey() });

    public static async Task<LoanResponse> CreateLoanAsync(this HttpClient client, Guid bookId, Guid userId)
    {
        var response = await client.RequestLoanAsync(bookId, userId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsAsync<LoanResponse>();
    }

    public static async Task<(BookResponse Book, LoanResponse Loan)> CreateBookWithLoanAsync(this HttpClient client, int totalCopies = 1)
    {
        var book = await client.CreateBookAsync(BookApi.NewCreateRequest(totalCopies: totalCopies));
        var user = await client.CreateUserAsync();
        return (book, await client.CreateLoanAsync(book.Id, user.Id));
    }

    public static async Task<int> ReadAvailableCopiesAsync(this ApiFactory factory, Guid bookId)
    {
        await using var context = factory.CreateDbContext();

        return await context.Books
            .Where(book => book.Id == bookId)
            .Select(book => book.AvailableCopies)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    public static async Task<int> CountLoansAsync(this ApiFactory factory, Guid bookId)
    {
        await using var context = factory.CreateDbContext();

        return await context.Loans.CountAsync(loan => loan.BookId == bookId, TestContext.Current.CancellationToken);
    }
}
