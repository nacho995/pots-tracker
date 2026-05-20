using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pots.Domain.Entities;

namespace Pots.Infrastructure.EntityConfigurations;

internal sealed class CaregiverNoteConfiguration : IEntityTypeConfiguration<CaregiverNote>
{
    public void Configure(EntityTypeBuilder<CaregiverNote> builder)
    {
        builder.ToTable("caregiver_notes");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.PatientId).IsRequired();
        builder.Property(n => n.AuthorUserId).IsRequired();
        builder.Property(n => n.Body).IsRequired().HasMaxLength(2000);
        builder.Property(n => n.CreatedAt).IsRequired();
        builder.Property(n => n.DeletedAt);
        builder.Property(n => n.DeletedByUserId);

        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(n => n.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(n => n.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(n => n.DeletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Hot-path query: "the active thread for this patient, newest first."
        // Index covers the WHERE patient_id + (filtered) deleted_at IS NULL +
        // ORDER BY created_at DESC pattern.
        builder.HasIndex(n => new { n.PatientId, n.CreatedAt })
            .HasDatabaseName("ix_caregiver_notes_patient_created");
    }
}
