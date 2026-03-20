using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skojjt.Core.Entities;

namespace Skojjt.Infrastructure.Data.Configurations;

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(e => e.KeyHash)
            .HasColumnName("key_hash")
            .HasMaxLength(64) // SHA256 hex = 64 chars
            .IsRequired();

        builder.Property(e => e.KeyPrefix)
            .HasColumnName("key_prefix")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(e => e.LastUsedAt)
            .HasColumnName("last_used_at");

        builder.Property(e => e.IsRevoked)
            .HasColumnName("is_revoked")
            .HasDefaultValue(false);

        builder.Property(e => e.RevokedAt)
            .HasColumnName("revoked_at");

        // Index on hash for fast lookup during validation
        builder.HasIndex(e => e.KeyHash)
            .IsUnique();

        // Ignore computed property
        builder.Ignore(e => e.IsValid);
    }
}
