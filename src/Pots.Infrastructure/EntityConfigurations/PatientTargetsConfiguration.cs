using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pots.Domain.Entities;

namespace Pots.Infrastructure.EntityConfigurations;

internal sealed class PatientTargetsConfiguration : IEntityTypeConfiguration<PatientTargets>
{
    public void Configure(EntityTypeBuilder<PatientTargets> builder)
    {
        builder.ToTable("patient_targets");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PatientId).IsRequired();

        // 1:1 with Patient.
        builder.HasIndex(e => e.PatientId).IsUnique();

        builder.Property(e => e.HydrationTargetMl);
        builder.Property(e => e.SaltTargetEnabled);
        builder.Property(e => e.SaltTargetMg);
        builder.Property(e => e.SaltClinicianAttestation).HasMaxLength(2000);
        builder.Property(e => e.CompressionGoalHoursPerDay);
        builder.Property(e => e.ExercisePlanNote).HasMaxLength(2000);
        builder.Property(e => e.SleepTargetHours).HasColumnType("numeric(4,1)");
        builder.Property(e => e.Language).HasMaxLength(8).IsRequired();
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.UpdatedAt);

        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(e => e.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
