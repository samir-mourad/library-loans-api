using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Api.Infrastructure.Idempotency;

/// <summary>
/// A processed (or in-flight, inside its own transaction) idempotent request and the response it produced.
/// </summary>
public sealed class IdempotencyRecord
{
    public required string Scope { get; init; }

    public required string Key { get; init; }

    public required string RequestHash { get; init; }

    public int? StatusCode { get; init; }

    public string? ResponseBody { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    private const int ScopeMaxLength = 50;
    private const int HashLength = 64;

    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_keys");
        builder.HasKey(record => new { record.Scope, record.Key });
        builder.Property(record => record.Scope).HasMaxLength(ScopeMaxLength);
        builder.Property(record => record.Key).HasMaxLength(IdempotencyKey.MaxLength);
        builder.Property(record => record.RequestHash).HasMaxLength(HashLength).IsFixedLength();
        builder.Property(record => record.ResponseBody).HasColumnType("jsonb");
        builder.HasIndex(record => record.CreatedAt);
    }
}
