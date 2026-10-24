namespace Library.Api.Infrastructure.Configuration;

/// <summary>
/// UTC clock truncated to microseconds, the precision of PostgreSQL <c>timestamptz</c>. Values returned to clients
/// right after a write are then identical to what is read back later.
/// </summary>
internal sealed class DatabasePrecisionTimeProvider(TimeProvider inner) : TimeProvider
{
    private const long TicksPerMicrosecond = TimeSpan.TicksPerMicrosecond;

    public override DateTimeOffset GetUtcNow()
    {
        var now = inner.GetUtcNow();
        return new DateTimeOffset(now.UtcTicks - now.UtcTicks % TicksPerMicrosecond, TimeSpan.Zero);
    }

    public override long GetTimestamp() => inner.GetTimestamp();

    public override long TimestampFrequency => inner.TimestampFrequency;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
