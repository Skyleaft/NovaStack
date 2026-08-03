using Identity.Domain.Aggregates;
using Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => UserId.From(value));

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(u => u.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(u => u.IsEmailVerified)
            .HasColumnName("is_email_verified")
            .HasDefaultValue(false);

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(u => u.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName("ix_users_email");

        // Map via join entity table explicitly to avoid implicit skip navigation issues
        builder.HasMany(u => u.Roles)
            .WithMany(r => r.Users)
            .UsingEntity<UserRole>(
                r => r.HasOne<Role>().WithMany().HasForeignKey(ur => ur.RoleId),
                l => l.HasOne<User>().WithMany().HasForeignKey(ur => ur.UserId)
            );

        builder.Navigation(u => u.Roles)
            .HasField("_roles")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Ignore domain events collection from persistence
        builder.Ignore(u => u.DomainEvents);
    }
}
