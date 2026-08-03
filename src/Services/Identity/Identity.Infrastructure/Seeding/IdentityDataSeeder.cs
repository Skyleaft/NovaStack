using Identity.Domain.Aggregates;
using Identity.Domain.ValueObjects;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Seeding;

/// <summary>
/// Seeds the initial Admin role and the <c>sysadmin</c> super-user account
/// on first startup when the database is empty.
///
/// Defaults:
///   Email    : sysadmin@novastack.local
///   Password : @superuser
///   Role     : Admin  (system role, cannot be deleted)
/// </summary>
public static class IdentityDataSeeder
{
    private const string AdminRoleName = "Admin";
    private const string SysAdminEmail = "sysadmin@novastack.local";
    private const string SysAdminPassword = "@superuser";

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILogger<IdentityDataSeederMarker>>();

        // ── Seed Admin role ───────────────────────────────────────────────
        var adminRole = await db.Roles
            .FirstOrDefaultAsync(r => r.Name == AdminRoleName);

        if (adminRole is null)
        {
            adminRole = Role.Create(RoleId.New(), AdminRoleName,
                "Full system administration access.", isSystemRole: true);
            await db.Roles.AddAsync(adminRole);
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded system role '{Role}'.", AdminRoleName);
        }

        // ── Seed sysadmin user ────────────────────────────────────────────
        var exists = await db.Users.AnyAsync(u => u.Email == SysAdminEmail);
        if (exists)
        {
            logger.LogDebug("Admin user '{Email}' already exists — skipping seed.", SysAdminEmail);
            return;
        }

        var userId = UserId.New();
        var passwordHash = hasher.HashPassword(null!, SysAdminPassword);

        var sysAdmin = User.Create(userId, SysAdminEmail, passwordHash, "System", "Admin");
        await db.Users.AddAsync(sysAdmin);
        await db.SaveChangesAsync();

        // ── Assign Admin role to sysadmin ─────────────────────────────────
        // Load with navigation so EF tracks the join row
        var userWithRoles = await db.Users
            .Include(u => u.Roles)
            .FirstAsync(u => u.Id == userId);

        userWithRoles.AssignRole(adminRole);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Seeded initial admin user '{Email}' with role '{Role}'.",
            SysAdminEmail, AdminRoleName);
    }
}

/// <summary>Marker type for <see cref="ILogger"/> category.</summary>
internal sealed class IdentityDataSeederMarker { }
