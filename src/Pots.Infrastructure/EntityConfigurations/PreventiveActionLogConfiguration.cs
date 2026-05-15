using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pots.Domain.Entities;

namespace Pots.Infrastructure.EntityConfigurations;

internal sealed class PreventiveActionLogConfiguration : IEntityTypeConfiguration<PreventiveActionLog>
{
    public void Configure(EntityTypeBuilder<PreventiveActionLog> builder)
    {
        builder.ToTable("preventive_action_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PatientId).IsRequired();
        builder.Property(e => e.Day).IsRequired();
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.UpdatedAt);

        builder.Property(e => e.UrineColor)
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(e => e.CaffeineLevel)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(e => e.ExerciseIntensity).HasMaxLength(50);
        builder.Property(e => e.PostExerciseSymptoms).HasMaxLength(500);
        builder.Property(e => e.MobilityAid).HasMaxLength(100);
        builder.Property(e => e.SideEffects).HasMaxLength(500);
        builder.Property(e => e.NewMedicationOrSupplement).HasMaxLength(200);

        // One row per patient per day. The endpoint upserts.
        builder.HasIndex(e => new { e.PatientId, e.Day }).IsUnique();

        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(e => e.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
