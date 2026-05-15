using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pots.Domain.Entities;

namespace Pots.Infrastructure.EntityConfigurations;

internal sealed class VitalSignLogConfiguration : IEntityTypeConfiguration<VitalSignLog>
{
    public void Configure(EntityTypeBuilder<VitalSignLog> builder)
    {
        builder.ToTable("vital_sign_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PatientId).IsRequired();
        builder.Property(e => e.RecordedAt);
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.WeightKg).HasColumnType("numeric(5,2)");
        builder.Property(e => e.AmbientTempC).HasColumnType("numeric(4,1)");

        builder.HasIndex(e => new { e.PatientId, e.RecordedAt }).IsDescending(false, true);

        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(e => e.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
