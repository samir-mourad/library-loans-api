using Library.Domain.Common;
using Library.Domain.Loans;

namespace Library.UnitTests.Loans;

public class LoanTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Period = TimeSpan.FromDays(14);

    [Fact]
    public void Create_starts_active_and_due_after_the_loan_period()
    {
        var bookId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        var loan = Loan.Create(bookId, userId, Now, Period);

        Assert.Equal(bookId, loan.BookId);
        Assert.Equal(userId, loan.UserId);
        Assert.Equal(LoanStatus.Active, loan.Status);
        Assert.Equal(Now, loan.LoanedAt);
        Assert.Equal(Now.AddDays(14), loan.DueAt);
        Assert.Null(loan.ReturnedAt);
        Assert.Null(loan.CancelledAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_requires_a_positive_loan_period(int days)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Loan.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Now, TimeSpan.FromDays(days)));
    }

    [Fact]
    public void Returning_an_active_loan_records_the_return()
    {
        var loan = NewLoan();

        var result = loan.Return(Now.AddDays(3));

        Assert.Equal(new FieldChange(LoanStatus.Active, LoanStatus.Returned), result.Value);
        Assert.Equal(LoanStatus.Returned, loan.Status);
        Assert.Equal(Now.AddDays(3), loan.ReturnedAt);
    }

    [Fact]
    public void Returning_twice_is_rejected()
    {
        var loan = NewLoan();
        loan.Return(Now.AddDays(1));

        var result = loan.Return(Now.AddDays(2));

        Assert.Equal(LoanErrors.NotActive, result.Error);
        Assert.Equal(Now.AddDays(1), loan.ReturnedAt);
    }

    [Fact]
    public void Cancelling_an_active_loan_keeps_the_trimmed_reason()
    {
        var loan = NewLoan();

        var result = loan.Cancel(Now.AddHours(1), "  registered by mistake ");

        Assert.Equal(new FieldChange(LoanStatus.Active, LoanStatus.Cancelled), result.Value);
        Assert.Equal(LoanStatus.Cancelled, loan.Status);
        Assert.Equal(Now.AddHours(1), loan.CancelledAt);
        Assert.Equal("registered by mistake", loan.CancellationReason);
    }

    [Fact]
    public void Blank_cancellation_reason_is_stored_as_no_reason()
    {
        var loan = NewLoan();

        loan.Cancel(Now, "   ");

        Assert.Null(loan.CancellationReason);
    }

    [Fact]
    public void Cancelling_a_returned_loan_is_rejected()
    {
        var loan = NewLoan();
        loan.Return(Now.AddDays(1));

        var result = loan.Cancel(Now.AddDays(2), null);

        Assert.Equal(LoanErrors.NotActive, result.Error);
        Assert.Equal(LoanStatus.Returned, loan.Status);
    }

    [Fact]
    public void Cancellation_reason_longer_than_maximum_is_rejected()
    {
        var loan = NewLoan();

        var result = loan.Cancel(Now, new string('a', Loan.CancellationReasonMaxLength + 1));

        Assert.Equal(LoanErrors.InvalidCancellationReason, result.Error);
        Assert.Equal(LoanStatus.Active, loan.Status);
    }

    [Fact]
    public void Active_loan_is_overdue_only_after_the_due_date()
    {
        var loan = NewLoan();

        Assert.False(loan.IsOverdue(loan.DueAt));
        Assert.True(loan.IsOverdue(loan.DueAt.AddTicks(1)));
    }

    [Fact]
    public void Returned_loan_is_never_overdue()
    {
        var loan = NewLoan();
        loan.Return(Now.AddDays(1));

        Assert.False(loan.IsOverdue(Now.AddDays(30)));
    }

    private static Loan NewLoan() => Loan.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Now, Period);
}
