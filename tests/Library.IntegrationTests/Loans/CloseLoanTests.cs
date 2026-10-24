using System.Net;
using System.Text.Json;
using Library.Api.Features.Audit;
using Library.Api.Features.Loans;
using Library.Domain.Loans;
using Library.IntegrationTests.Infrastructure;

namespace Library.IntegrationTests.Loans;

public class CloseLoanTests(ApiFactory factory)
{
    [Fact]
    public async Task Returning_a_loan_restores_availability_and_records_an_event()
    {
        using var client = factory.CreateClient();
        var (book, loan) = await client.CreateBookWithLoanAsync(totalCopies: 1);

        var response = await client.SendJsonAsync(HttpMethod.Post, LoanApi.ReturnPath(loan.Id), body: null);
        var returned = await response.ReadAsAsync<LoanResponse>();
        var auditEvent = (await factory.ReadAuditEventsAsync(loan.Id))[^1];
        using var data = JsonDocument.Parse(auditEvent.Data);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(LoanStatus.Returned, returned.Status);
        Assert.NotNull(returned.ReturnedAt);
        Assert.Equal(1, await factory.ReadAvailableCopiesAsync(book.Id));
        Assert.Equal(AuditActions.LoanReturned, auditEvent.Action);
        Assert.Equal("Active", data.RootElement.GetProperty("status").GetProperty("from").GetString());
        Assert.Equal("Returned", data.RootElement.GetProperty("status").GetProperty("to").GetString());
    }

    [Fact]
    public async Task Cancelling_a_loan_keeps_the_reason_and_restores_availability()
    {
        using var client = factory.CreateClient();
        var (book, loan) = await client.CreateBookWithLoanAsync(totalCopies: 1);

        var response = await client.SendJsonAsync(HttpMethod.Post, LoanApi.CancelPath(loan.Id), new CancelLoanRequest { Reason = "Registered by mistake" });
        var cancelled = await response.ReadAsAsync<LoanResponse>();
        var auditEvent = (await factory.ReadAuditEventsAsync(loan.Id))[^1];
        using var data = JsonDocument.Parse(auditEvent.Data);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(LoanStatus.Cancelled, cancelled.Status);
        Assert.Equal("Registered by mistake", cancelled.CancellationReason);
        Assert.Equal(1, await factory.ReadAvailableCopiesAsync(book.Id));
        Assert.Equal(AuditActions.LoanCancelled, auditEvent.Action);
        Assert.Equal("Registered by mistake", data.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Cancelling_without_a_body_is_allowed()
    {
        using var client = factory.CreateClient();
        var (_, loan) = await client.CreateBookWithLoanAsync();

        var response = await client.SendJsonAsync(HttpMethod.Post, LoanApi.CancelPath(loan.Id), body: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await response.ReadAsAsync<LoanResponse>()).CancellationReason);
    }

    [Fact]
    public async Task Returning_twice_returns_409_and_restores_the_copy_once()
    {
        using var client = factory.CreateClient();
        var (book, loan) = await client.CreateBookWithLoanAsync(totalCopies: 2);
        await client.SendJsonAsync(HttpMethod.Post, LoanApi.ReturnPath(loan.Id), body: null);

        var response = await client.SendJsonAsync(HttpMethod.Post, LoanApi.ReturnPath(loan.Id), body: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("loan.not_active", await response.ReadProblemCodeAsync());
        Assert.Equal(2, await factory.ReadAvailableCopiesAsync(book.Id));
    }

    [Fact]
    public async Task Cancelling_a_returned_loan_returns_409()
    {
        using var client = factory.CreateClient();
        var (_, loan) = await client.CreateBookWithLoanAsync();
        await client.SendJsonAsync(HttpMethod.Post, LoanApi.ReturnPath(loan.Id), body: null);

        var response = await client.SendJsonAsync(HttpMethod.Post, LoanApi.CancelPath(loan.Id), body: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("loan.not_active", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Returned_loan_frees_the_user_to_borrow_the_same_book_again()
    {
        using var client = factory.CreateClient();
        var book = await client.CreateBookAsync(BookApi.NewCreateRequest(totalCopies: 1));
        var user = await client.CreateUserAsync();
        var first = await client.CreateLoanAsync(book.Id, user.Id);
        await client.SendJsonAsync(HttpMethod.Post, LoanApi.ReturnPath(first.Id), body: null);

        var response = await client.RequestLoanAsync(book.Id, user.Id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Closing_an_unknown_loan_returns_404()
    {
        using var client = factory.CreateClient();

        var response = await client.SendJsonAsync(HttpMethod.Post, LoanApi.ReturnPath(Guid.CreateVersion7()), body: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("loan.not_found", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Cancellation_reason_above_maximum_returns_400()
    {
        using var client = factory.CreateClient();
        var (_, loan) = await client.CreateBookWithLoanAsync();

        var response = await client.SendJsonAsync(
            HttpMethod.Post,
            LoanApi.CancelPath(loan.Id),
            new CancelLoanRequest { Reason = new string('a', Loan.CancellationReasonMaxLength + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
