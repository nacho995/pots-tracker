using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pots.Domain.Entities;

namespace Pots.Infrastructure.EntityConfigurations;

internal sealed class SymptomLogConfiguration : IEntityTypeConfiguration<SymptomLog>
{
    public void Configure(EntityTypeBuilder<SymptomLog> builder)
    {
        builder.ToTable("symptom_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PatientId).IsRequired();
        builder.Property(e => e.RecordedByUserId).IsRequired();
        builder.Property(e => e.RecordedAt);
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.Bowel)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.HasIndex(e => new { e.PatientId, e.RecordedAt }).IsDescending(false, true);

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
