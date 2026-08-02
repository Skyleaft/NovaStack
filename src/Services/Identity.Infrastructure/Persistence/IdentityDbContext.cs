using Identity.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using NovaStack.Infrastructure.Persistence;
using NovaStack.SharedKernel.Abstractions;

namespace Identity.Infrastructure.Persistence;

/// <summary>EF Core DbContext for the Identity service. Also acts as the Unit of Work.</summary>
public sealed class IdentityDbContext : DbContextBase, IUnitOfWork
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        modelBuilder.HasDefaultSchema("identity");
    }
}
