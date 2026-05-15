using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pots.Domain.Entities;

namespace Pots.Infrastructure.EntityConfigurations;

internal sealed class MagicLinkTokenConfiguration : IEntityTypeConfiguration<MagicLinkToken>
{
    public void Configure(EntityTypeBuilder<MagicLinkToken> builder)
    {
        builder.ToTable("magic_link_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Email)
            .HasColumnType("citext")
            .IsRequired();
        builder.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(64); // SHA-256 hex
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.Property(t => t.ExpiresAt);
        builder.Property(t => t.ConsumedAt);
        builder.Property(t => t.CreatedAt);
        // Index to prune expired/consumed tokens efficiently.
        builder.HasIndex(t => t.ExpiresAt);
    }
}
