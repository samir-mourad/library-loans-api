using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Api.Features.Audit;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(auditEvent => auditEvent.Id);
        builder.Property(auditEvent => auditEvent.Id).ValueGeneratedNever();
        builder.Property(auditEvent => auditEvent.EntityType).HasMaxLength(50);
        builder.Property(auditEvent => auditEvent.Action).HasMaxLength(100);
        builder.Property(auditEvent => auditEvent.Actor).HasMaxLength(100);
        builder.Property(auditEvent => auditEvent.CorrelationId).HasMaxLength(128);
        builder.Property(auditEvent => auditEvent.Data).HasColumnType("jsonb");

        builder.HasIndex(auditEvent => new { auditEvent.EntityType, auditEvent.EntityId, auditEvent.OccurredAt });
        builder.HasIndex(auditEvent => auditEvent.OccurredAt);
    }
}
