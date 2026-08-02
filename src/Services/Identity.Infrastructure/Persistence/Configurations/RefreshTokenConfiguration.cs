using Identity.Domain.Aggregates;
using Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => RefreshTokenId.From(value));

        builder.Property(rt => rt.Token)
            .HasColumnName("token")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(rt => rt.UserId)
            .HasColumnName("user_id")
            .HasConversion(id => id.Value, value => UserId.From(value))
            .IsRequired();

        builder.Property(rt => rt.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(rt => rt.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(rt => rt.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(rt => rt.IsRevoked)
            .HasColumnName("is_revoked")
            .HasDefaultValue(false);

        builder.HasIndex(rt => rt.Token).IsUnique().HasDatabaseName("ix_refresh_tokens_token");
        builder.HasIndex(rt => rt.UserId).HasDatabaseName("ix_refresh_tokens_user_id");

        builder.Ignore(rt => rt.IsExpired);
        builder.Ignore(rt => rt.IsActive);
        builder.Ignore(rt => rt.DomainEvents);
    }
}
