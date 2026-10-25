using System.Diagnostics.Metrics;
using Library.Api.Infrastructure.Observability;

namespace Library.Api.Features.Loans;

/// <summary>
/// Business metrics required for loans, exported through OpenTelemetry under the <c>Library.Api</c> meter.
/// Counters record only decided outcomes (after commit or after the rejection is final).
/// </summary>
public sealed class LoanMetrics
{
    private static readonly InstrumentAdvice<double> DurationAdvice = new()
    {
        HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5]
    };

    private readonly Counter<long> _created;
    private readonly Counter<long> _unavailable;
    private readonly Counter<long> _idempotentReplays;
    private readonly Histogram<double> _requestDuration;

    public LoanMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(Telemetry.MeterName);
        _created = meter.CreateCounter<long>("library.loans.created", "{loan}", "Loans created.");
        _unavailable = meter.CreateCounter<long>("library.loans.unavailable", "{loan}", "Loan requests rejected because no copy was available.");
        _idempotentReplays = meter.CreateCounter<long>("library.loans.idempotent_replays", "{request}", "Loan requests answered from a stored idempotent response.");
        _requestDuration = meter.CreateHistogram("library.loans.request.duration", "s", "Duration of loan creation requests.", advice: DurationAdvice);
    }

    public void LoanCreated() => _created.Add(1);

    public void LoanRejectedAsUnavailable() => _unavailable.Add(1);

    public void IdempotentReplay() => _idempotentReplays.Add(1);

    public void RecordRequestDuration(TimeSpan elapsed) => _requestDuration.Record(elapsed.TotalSeconds);
}
