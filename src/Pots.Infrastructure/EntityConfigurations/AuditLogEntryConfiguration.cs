using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pots.Domain.Entities;

namespace Pots.Infrastructure.EntityConfigurations;

// TODO(task-5/RLS): enforce append-only at the DB level by revoking
// UPDATE/DELETE on audit_log from the application role, plus a trigger
// that raises on UPDATE OR DELETE. The PotsDbContext override is the
// first line of defense; the DB trigger is the floor.
internal sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_log");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ActorUserId).IsRequired();
        builder.Property(e => e.PatientId);
        builder.Property(e => e.Action).IsRequired().HasMaxLength(64);
        builder.Property(e => e.EntityType).IsRequired().HasMaxLength(64);
        builder.Property(e => e.EntityId);
        builder.Property(e => e.ChangesJson).HasColumnType("jsonb");
        builder.Property(e => e.CreatedAt);

        builder.HasIndex(e => new { e.PatientId, e.CreatedAt })
            .IsDescending(false, true);
        builder.HasIndex(e => new { e.ActorUserId, e.CreatedAt })
            .IsDescending(false, true);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict (not soft-reference) is deliberate: audit retention overrides
        // erasure for access/grant actions under GDPR Art. 17(3)(b)/(e) (legal
        // obligation + legitimate interest in security audit trails). Any GDPR
        // erasure flow must explicitly handle audit_log via the retention policy,
        // not via DB cascade.
        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(e => e.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
