using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pots.Domain.Entities;

namespace Pots.Infrastructure.EntityConfigurations;

internal sealed class GrantUpgradeRequestConfiguration : IEntityTypeConfiguration<GrantUpgradeRequest>
{
    public void Configure(EntityTypeBuilder<GrantUpgradeRequest> builder)
    {
        builder.ToTable("grant_upgrade_requests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.GrantId).IsRequired();
        builder.Property(r => r.RequesterUserId).IsRequired();
        builder.Property(r => r.PatientId).IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.Message).HasMaxLength(500);
        builder.Property(r => r.RequestedAt).IsRequired();
        builder.Property(r => r.ResolvedAt);
        builder.Property(r => r.ResolvedByUserId);

        // FKs are Restrict on delete to keep the request history intact.
        // Hard-deleting the underlying grant or its participants without
        // first cleaning up requests would lose audit signal.
        builder.HasOne<PatientGrant>()
            .WithMany()
            .HasForeignKey(r => r.GrantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(r => r.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.RequesterUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.ResolvedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // At most one Pending request per grant. A denied/cancelled/approved
        // row is OK to coexist (history); the partial filter limits the
        // uniqueness to in-flight requests. The status string MUST match the
        // domain enum literal exactly — see GrantUpgradeRequestStatus.
        builder.HasIndex(r => r.GrantId)
            .IsUnique()
            .HasFilter("status = 'Pending'")
            .HasDatabaseName("ix_grant_upgrade_requests_grant_one_pending");
    }
}
