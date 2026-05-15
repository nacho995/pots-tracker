using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pots.Domain.Entities;

namespace Pots.Infrastructure.EntityConfigurations;

internal sealed class EpisodeConfiguration : IEntityTypeConfiguration<Episode>
{
    public void Configure(EntityTypeBuilder<Episode> builder)
    {
        builder.ToTable("episodes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PatientId).IsRequired();
        builder.Property(e => e.StartTime).IsRequired();
        builder.Property(e => e.CreatedAt);

        builder.Property(e => e.MainSymptom).HasMaxLength(200);
        builder.Property(e => e.ActionTaken).HasMaxLength(500);
        builder.Property(e => e.Note).HasMaxLength(1000);

        builder.Property(e => e.PostureBefore)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(e => e.TriggerSuspected)
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.HasIndex(e => new { e.PatientId, e.StartTime }).IsDescending(false, true);

        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(e => e.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
