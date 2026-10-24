using Library.Domain.Loans;

namespace Library.Api.Features.Loans;

/// <summary>
/// Structured loan logs carrying only business identifiers, never personal data.
/// </summary>
internal static partial class LoanLog
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Loan {LoanId} created for book {BookId} and user {UserId}")]
    public static partial void Created(ILogger logger, Guid loanId, Guid bookId, Guid userId);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Loan rejected for book {BookId} and user {UserId}: {ReasonCode}")]
    public static partial void Rejected(ILogger logger, Guid bookId, Guid userId, string reasonCode);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Loan {LoanId} of book {BookId} closed as {Status}")]
    public static partial void Closed(ILogger logger, Guid loanId, Guid bookId, LoanStatus status);
}
