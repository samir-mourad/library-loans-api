using System.Net;
using Library.Api.Features.Audit;
using Library.Api.Features.Loans;
using Library.Api.Infrastructure.Pagination;
using Library.Domain.Loans;
using Library.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Library.IntegrationTests.Loans;

public class LoanQueryTests(ApiFactory factory)
{
    [Fact]
    public async Task Availability_reflects_active_loans()
    {
        using var client = factory.CreateClient();
        var (book, _) = await client.CreateBookWithLoanAsync(totalCopies: 3);

        var response = await client.SendGetAsync(LoanApi.AvailabilityPath(book.Id));
        var availability = await response.ReadAsAsync<AvailabilityResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new AvailabilityResponse(book.Id, 3, 2, 1, true, true), availability);
    }

    [Fact]
    public async Task Book_without_free_copies_is_not_available()
    {
        using var client = factory.CreateClient();
        var (book, _) = await client.CreateBookWithLoanAsync(totalCopies: 1);

        var availability = await (await client.SendGetAsync(LoanApi.AvailabilityPath(book.Id))).ReadAsAsync<AvailabilityResponse>();

        Assert.False(availability.IsAvailable);
        Assert.Equal(0, availability.AvailableCopies);
    }

    [Fact]
    public async Task Availability_of_unknown_book_returns_404()
    {
        using var client = factory.CreateClient();

        var response = await client.SendGetAsync(LoanApi.AvailabilityPath(Guid.CreateVersion7()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("book.not_found", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task History_is_preserved_after_return_and_cancellation()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync(BookApi.NewCreateRequest(totalCopies: 2));
        var ana = await client.CreateUserAsync();
        var bruno = await client.CreateUserAsync();
        var returned = await client.CreateLoanAsync(book.Id, ana.Id);
        await client.SendJsonAsync(HttpMethod.Post, LoanApi.ReturnPath(returned.Id), body: null);
        var cancelled = await client.CreateLoanAsync(book.Id, bruno.Id);
        await client.SendJsonAsync(HttpMethod.Post, LoanApi.CancelPath(cancelled.Id), new CancelLoanRequest { Reason = "Not picked up" });
        var active = await client.CreateLoanAsync(book.Id, ana.Id);

        var bookHistory = await (await client.SendGetAsync(LoanApi.BookHistoryPath(book.Id))).ReadAsAsync<PagedResponse<LoanResponse>>();
        var anaLoans = await (await client.SendGetAsync(LoanApi.UserLoansPath(ana.Id))).ReadAsAsync<PagedResponse<LoanResponse>>();
        var returnedAudit = await factory.ReadAuditEventsAsync(returned.Id);
        var cancelledAudit = await factory.ReadAuditEventsAsync(cancelled.Id);

        Assert.Equal([active.Id, cancelled.Id, returned.Id], bookHistory.Items.Select(loan => loan.Id));
        Assert.Equal([LoanStatus.Active, LoanStatus.Cancelled, LoanStatus.Returned], bookHistory.Items.Select(loan => loan.Status));
        Assert.Equal("Not picked up", bookHistory.Items[1].CancellationReason);
        Assert.Equal([active.Id, returned.Id], anaLoans.Items.Select(loan => loan.Id));
        Assert.Equal([AuditActions.LoanCreated, AuditActions.LoanReturned], returnedAudit.Select(auditEvent => auditEvent.Action));
        Assert.Equal([AuditActions.LoanCreated, AuditActions.LoanCancelled], cancelledAudit.Select(auditEvent => auditEvent.Action));
    }

    [Fact]
    public async Task User_loans_can_be_filtered_by_status()
    {
        using var client = factory.CreateClient();
        var user = await client.CreateUserAsync();
        var firstBook = await client.CreateBookAsync();
        var secondBook = await client.CreateBookAsync();
        var returned = await client.CreateLoanAsync(firstBook.Id, user.Id);
        await client.SendJsonAsync(HttpMethod.Post, LoanApi.ReturnPath(returned.Id), body: null);
        await client.CreateLoanAsync(secondBook.Id, user.Id);

        var response = await client.SendGetAsync($"{LoanApi.UserLoansPath(user.Id)}?status=Returned");
        var page = await response.ReadAsAsync<PagedResponse<LoanResponse>>();

        Assert.Equal(returned.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task History_of_unknown_book_or_user_returns_404()
    {
        using var client = factory.CreateClient();

        var bookResponse = await client.SendGetAsync(LoanApi.BookHistoryPath(Guid.CreateVersion7()));
        var userResponse = await client.SendGetAsync(LoanApi.UserLoansPath(Guid.CreateVersion7()));

        Assert.Equal("book.not_found", await bookResponse.ReadProblemCodeAsync());
        Assert.Equal("user.not_found", await userResponse.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Loan_becomes_overdue_after_the_due_date()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero));
        await using var app = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        using var client = app.CreateClient();
        var (_, loan) = await client.CreateBookWithLoanAsync();

        var beforeDue = await (await client.SendGetAsync(LoanApi.LoanPath(loan.Id))).ReadAsAsync<LoanResponse>();
        clock.Advance(TimeSpan.FromDays(15));
        var afterDue = await (await client.SendGetAsync(LoanApi.LoanPath(loan.Id))).ReadAsAsync<LoanResponse>();

        Assert.False(beforeDue.IsOverdue);
        Assert.True(afterDue.IsOverdue);
    }
}
