using System.Diagnostics.Metrics;
using Library.Api.Infrastructure.Observability;
using Library.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace Library.IntegrationTests.Loans;

/// <summary>
/// Each test runs its own host: every DI container has its own <see cref="IMeterFactory"/>, so measurements from
/// tests running in parallel never leak into these assertions.
/// </summary>
public class LoanMetricsTests(ApiFactory factory)
{
    private const string Created = "library.loans.created";
    private const string Unavailable = "library.loans.unavailable";
    private const string IdempotentReplays = "library.loans.idempotent_replays";
    private const string RequestDuration = "library.loans.request.duration";

    [Fact]
    public async Task Created_loan_is_counted_and_its_duration_recorded()
    {
        await using var app = factory.WithWebHostBuilder(_ => { });
        using var created = Collect<long>(app, Created);
        using var duration = Collect<double>(app, RequestDuration);
        using var client = app.CreateClient();

        await client.CreateBookWithLoanAsync();

        Assert.Equal(1, Sum(created));
        var measurement = Assert.Single(duration.GetMeasurementSnapshot());
        Assert.True(measurement.Value > 0);
    }

    [Fact]
    public async Task Rejection_for_lack_of_copies_is_counted()
    {
        await using var app = factory.WithWebHostBuilder(_ => { });
        using var client = app.CreateClient();
        var (book, _) = await client.CreateBookWithLoanAsync(totalCopies: 1);
        var otherUser = await client.CreateUserAsync();
        using var unavailable = Collect<long>(app, Unavailable);

        await client.RequestLoanAsync(book.Id, otherUser.Id);

        Assert.Equal(1, Sum(unavailable));
    }

    [Fact]
    public async Task Idempotent_replay_is_counted_without_counting_a_new_loan()
    {
        await using var app = factory.WithWebHostBuilder(_ => { });
        using var created = Collect<long>(app, Created);
        using var replays = Collect<long>(app, IdempotentReplays);
        using var client = app.CreateClient();
        var book = await client.CreateBookAsync();
        var user = await client.CreateUserAsync();
        var key = LoanApi.NewIdempotencyKey();

        await client.RequestLoanAsync(book.Id, user.Id, key);
        await client.RequestLoanAsync(book.Id, user.Id, key);

        Assert.Equal(1, Sum(created));
        Assert.Equal(1, Sum(replays));
    }

    private static MetricCollector<T> Collect<T>(WebApplicationFactory<Program> app, string instrumentName) where T : struct =>
        new(app.Services.GetRequiredService<IMeterFactory>(), Telemetry.MeterName, instrumentName);

    private static long Sum(MetricCollector<long> collector) =>
        collector.GetMeasurementSnapshot().Sum(measurement => measurement.Value);
}
