using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");
        builder.HasKey(ur => new { ur.UserId, ur.RoleId });

        builder.Property(ur => ur.UserId)
            .HasColumnName("user_id")
            .HasConversion(id => id.Value, value => Identity.Domain.ValueObjects.UserId.From(value));

        builder.Property(ur => ur.RoleId)
            .HasColumnName("role_id")
            .HasConversion(id => id.Value, value => Identity.Domain.ValueObjects.RoleId.From(value));
    }
}
