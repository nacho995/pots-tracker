using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pots.Domain.Entities;

namespace Pots.Infrastructure.EntityConfigurations;

internal sealed class PatientGrantConfiguration : IEntityTypeConfiguration<PatientGrant>
{
    public void Configure(EntityTypeBuilder<PatientGrant> builder)
    {
        builder.ToTable("patient_grants");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.PatientId).IsRequired();
        builder.Property(g => g.GranteeUserId).IsRequired();
        builder.Property(g => g.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(g => g.GrantedAt);
        builder.Property(g => g.GrantedByUserId).IsRequired();
        builder.Property(g => g.RevokedAt);
        builder.Property(g => g.RevokedByUserId);

        builder.HasIndex(g => new { g.PatientId, g.GranteeUserId })
            .IsUnique()
            .HasFilter("revoked_at IS NULL");

        // Restrict on Patient delete: forces explicit revoke flow.
        // Hard-deleting a patient with active grants would destroy access
        // history that audit/compliance reviews need.
        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(g => g.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(g => g.GranteeUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(g => g.GrantedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(g => g.RevokedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
