using Identity.Domain.Aggregates;
using Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => RoleId.From(value));

        builder.Property(r => r.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(r => r.IsSystemRole)
            .HasColumnName("is_system_role")
            .HasDefaultValue(false);

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at");

        builder.HasIndex(r => r.Name).IsUnique().HasDatabaseName("ix_roles_name");

        builder.Ignore(r => r.DomainEvents);
        builder.Ignore(r => r.Permissions); // Stored as separate table via owned entity if needed

        builder.Navigation(r => r.Users)
            .HasField("_users")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
