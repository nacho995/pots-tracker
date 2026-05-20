using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pots.Domain.Entities;

namespace Pots.Infrastructure.EntityConfigurations;

internal sealed class DailyStatusEntryConfiguration : IEntityTypeConfiguration<DailyStatusEntry>
{
    public void Configure(EntityTypeBuilder<DailyStatusEntry> builder)
    {
        builder.ToTable("daily_status_entries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PatientId).IsRequired();
        builder.Property(e => e.RecordedByUserId).IsRequired();
        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(e => e.Posture)
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(e => e.Activity).HasMaxLength(100);
        builder.Property(e => e.LocationNote).HasMaxLength(200);
        builder.Property(e => e.Note).HasMaxLength(1000);
        builder.Property(e => e.EpisodeOccurred);
        builder.Property(e => e.CreatedAt);

        builder.HasIndex(e => new { e.PatientId, e.CreatedAt }).IsDescending(false, true);

        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(e => e.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.RecordedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
