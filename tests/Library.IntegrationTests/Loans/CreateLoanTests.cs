using System.Net;
using System.Text.Json;
using Library.Api.Features.Audit;
using Library.Api.Features.Loans;
using Library.Domain.Loans;
using Library.IntegrationTests.Infrastructure;

namespace Library.IntegrationTests.Loans;

public class CreateLoanTests(ApiFactory factory)
{
    [Fact]
    public async Task Lending_a_copy_returns_201_decrements_availability_and_records_an_event()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync(BookApi.NewCreateRequest(totalCopies: 2));
        var user = await client.CreateUserAsync();
        var headers = new Dictionary<string, string> { ["X-Actor-Id"] = "librarian-3", ["X-Correlation-Id"] = "lend-1" };

        var response = await client.SendJsonAsync(HttpMethod.Post, LoanApi.LoansPath, new CreateLoanRequest { BookId = book.Id, UserId = user.Id }, headers);
        var loan = await response.ReadAsAsync<LoanResponse>();
        var auditEvent = Assert.Single(await factory.ReadAuditEventsAsync(loan.Id));
        using var data = JsonDocument.Parse(auditEvent.Data);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(LoanApi.LoanPath(loan.Id), response.Headers.Location?.OriginalString);
        Assert.Equal(LoanStatus.Active, loan.Status);
        Assert.Equal(loan.LoanedAt.AddDays(14), loan.DueAt);
        Assert.False(loan.IsOverdue);
        Assert.Equal(1, await factory.ReadAvailableCopiesAsync(book.Id));
        Assert.Equal(AuditActions.LoanCreated, auditEvent.Action);
        Assert.Equal(AuditEntityTypes.Loan, auditEvent.EntityType);
        Assert.Equal("librarian-3", auditEvent.Actor);
        Assert.Equal("lend-1", auditEvent.CorrelationId);
        Assert.Equal(book.Id, data.RootElement.GetProperty("bookId").GetGuid());
        Assert.Equal(user.Id, data.RootElement.GetProperty("userId").GetGuid());
    }

    [Fact]
    public async Task Created_loan_can_be_read_back()
    {
        using var client = factory.CreateClient();
        var (_, loan) = await client.CreateBookWithLoanAsync();

        var response = await client.SendGetAsync(LoanApi.LoanPath(loan.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(loan, await response.ReadAsAsync<LoanResponse>());
    }

    [Fact]
    public async Task Unknown_book_returns_404()
    {
        using var client = factory.CreateClient();
        var user = await client.CreateUserAsync();

        var response = await client.RequestLoanAsync(Guid.CreateVersion7(), user.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("book.not_found", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Unknown_user_returns_404_and_keeps_availability()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync(BookApi.NewCreateRequest(totalCopies: 1));

        var response = await client.RequestLoanAsync(book.Id, Guid.CreateVersion7());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("user.not_found", await response.ReadProblemCodeAsync());
        Assert.Equal(1, await factory.ReadAvailableCopiesAsync(book.Id));
    }

    [Fact]
    public async Task Inactive_book_returns_409()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync();
        var user = await client.CreateUserAsync();
        await client.SendJsonAsync(HttpMethod.Delete, BookApi.BookPath(book.Id), body: null);

        var response = await client.RequestLoanAsync(book.Id, user.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("book.inactive", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Book_without_available_copies_returns_409()
    {
        using var client = factory.CreateClient();
        var (book, _) = await client.CreateBookWithLoanAsync(totalCopies: 1);
        var otherUser = await client.CreateUserAsync();

        var response = await client.RequestLoanAsync(book.Id, otherUser.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("book.unavailable", await response.ReadProblemCodeAsync());
        Assert.Equal(0, await factory.ReadAvailableCopiesAsync(book.Id));
    }

    [Fact]
    public async Task Second_active_loan_of_the_same_book_for_the_same_user_returns_409()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync(BookApi.NewCreateRequest(totalCopies: 3));
        var user = await client.CreateUserAsync();
        await client.CreateLoanAsync(book.Id, user.Id);

        var response = await client.RequestLoanAsync(book.Id, user.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("loan.duplicate_active", await response.ReadProblemCodeAsync());
        Assert.Equal(2, await factory.ReadAvailableCopiesAsync(book.Id));
    }

    [Fact]
    public async Task Unknown_loan_returns_404()
    {
        using var client = factory.CreateClient();

        var response = await client.SendGetAsync(LoanApi.LoanPath(Guid.CreateVersion7()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("loan.not_found", await response.ReadProblemCodeAsync());
    }
}
