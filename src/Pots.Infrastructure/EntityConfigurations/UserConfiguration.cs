using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pots.Domain.Entities;

namespace Pots.Infrastructure.EntityConfigurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        // citext: case-insensitive comparisons + uniqueness without lower(email)
        // helpers. Length is enforced in the domain factory (320 chars per RFC 5321).
        builder.Property(u => u.Email)
            .HasColumnType("citext")
            .HasMaxLength(320)
            .IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.DisplayName).HasMaxLength(100);
        builder.Property(u => u.CreatedAt);
    }
}
