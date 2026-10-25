using System.Text.Json;
using Library.Api.Features.Books;
using Library.Api.Infrastructure.ErrorHandling;
using Library.Api.Infrastructure.Idempotency;
using Library.Api.Infrastructure.Persistence;
using Library.Api.Infrastructure.Serialization;
using Library.Domain.Books;
using Library.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Features.Loans;

/// <summary>
/// Makes loan creation idempotent. One transaction claims the key, runs <see cref="CreateLoanHandler"/> behind a
/// savepoint and stores the outcome: success (201) or business rejection (404/409, partial work rolled back to the
/// savepoint). Unexpected failures roll back everything, key included, so the client can retry.
/// </summary>
public sealed class IdempotentCreateLoanHandler(
    LibraryDbContext dbContext,
    IdempotencyStore idempotencyStore,
    CreateLoanHandler createLoanHandler,
    BookCache bookCache,
    LoanMetrics loanMetrics,
    TimeProvider clock,
    ILogger<IdempotentCreateLoanHandler> logger)
{
    public const string Scope = "POST /loans";

    private const string SavepointName = "create_loan";

    public async Task<IdempotentOutcome<LoanResponse>> HandleAsync(
        string idempotencyKey,
        CreateLoanRequest request,
        CancellationToken cancellationToken)
    {
        var startedAt = clock.GetTimestamp();
        try
        {
            return await ProcessAsync(idempotencyKey, request, cancellationToken);
        }
        finally
        {
            loanMetrics.RecordRequestDuration(clock.GetElapsedTime(startedAt));
        }
    }

    private async Task<IdempotentOutcome<LoanResponse>> ProcessAsync(
        string idempotencyKey,
        CreateLoanRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestHash = IdempotencyKey.HashRequest($"{request.BookId:N}|{request.UserId:N}");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var start = await idempotencyStore.TryBeginAsync(Scope, idempotencyKey, requestHash, cancellationToken);
        switch (start.Kind)
        {
            case IdempotencyStartKind.Mismatch:
                return IdempotentOutcome<LoanResponse>.Fresh(IdempotencyErrors.KeyReused);
            case IdempotencyStartKind.Replay:
                LoanLog.IdempotentReplay(logger, idempotencyKey, start.StatusCode);
                loanMetrics.IdempotentReplay();
                return IdempotentOutcome<LoanResponse>.Replayed(Deserialize(start));
        }

        await transaction.CreateSavepointAsync(SavepointName, cancellationToken);
        var result = await createLoanHandler.HandleAsync(request, cancellationToken);

        if (result.IsFailure)
        {
            await transaction.RollbackToSavepointAsync(SavepointName, cancellationToken);
            dbContext.ChangeTracker.Clear();
        }

        var (statusCode, body) = Serialize(result);
        await idempotencyStore.CompleteAsync(Scope, idempotencyKey, statusCode, body, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (result.IsSuccess)
        {
            await bookCache.InvalidateAsync(result.Value.BookId);
            loanMetrics.LoanCreated();
            LoanLog.Created(logger, result.Value.Id, result.Value.BookId, result.Value.UserId);
        }
        else if (result.Error == BookErrors.Unavailable)
        {
            loanMetrics.LoanRejectedAsUnavailable();
        }

        return IdempotentOutcome<LoanResponse>.Fresh(result);
    }

    private static (int StatusCode, string Body) Serialize(Result<LoanResponse> result) =>
        result.IsSuccess
            ? (StatusCodes.Status201Created, JsonSerializer.Serialize(result.Value, JsonExtensions.StorageSerializerOptions))
            : (result.Error!.StatusCode(), JsonSerializer.Serialize(result.Error, JsonExtensions.StorageSerializerOptions));

    private static Result<LoanResponse> Deserialize(IdempotencyStart replay) =>
        replay.StatusCode == StatusCodes.Status201Created
            ? JsonSerializer.Deserialize<LoanResponse>(replay.ResponseBody, JsonExtensions.StorageSerializerOptions)!
            : JsonSerializer.Deserialize<Error>(replay.ResponseBody, JsonExtensions.StorageSerializerOptions)!;
}
