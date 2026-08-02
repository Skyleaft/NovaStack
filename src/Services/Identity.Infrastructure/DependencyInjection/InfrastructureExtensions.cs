using Identity.Domain.Aggregates;
using Identity.Domain.Repositories;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NovaStack.Infrastructure.Persistence.Options;
using NovaStack.SharedKernel.Abstractions;

namespace Identity.Infrastructure.DependencyInjection;

/// <summary>
/// Registers all Identity infrastructure services:
/// EF Core DbContext, Repositories, Unit of Work, password hasher, and Dapper factory.
/// </summary>
public static class InfrastructureExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddIdentityDatabase(configuration)
            .AddIdentityRepositories();

        return services;
    }

    // ── Database ──────────────────────────────────────────────────────────
    private static IServiceCollection AddIdentityDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbOptions = configuration
            .GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>() ?? new DatabaseOptions();

        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));

        services.AddDbContext<IdentityDbContext>(options =>
        {
            switch (dbOptions.Provider)
            {
                case DatabaseProvider.PostgreSQL:
                    options.UseNpgsql(dbOptions.ConnectionString, npgsql =>
                    {
                        npgsql.MigrationsHistoryTable("__ef_migrations_history", "identity");
                        npgsql.EnableRetryOnFailure(3);
                    });
                    break;

                case DatabaseProvider.SqlServer:
                    options.UseSqlServer(dbOptions.ConnectionString, sql =>
                    {
                        sql.MigrationsHistoryTable("__ef_migrations_history", "identity");
                        sql.EnableRetryOnFailure(3);
                    });
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported database provider: {dbOptions.Provider}");
            }

            if (dbOptions.EnableDetailedErrors)
                options.EnableDetailedErrors();

            if (dbOptions.EnableSensitiveDataLogging)
                options.EnableSensitiveDataLogging();
        });

        // Unit of Work
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IdentityDbContext>());

        // Dapper connection factory
        services.AddScoped<ISqlConnectionFactory, IdentitySqlConnectionFactory>();

        return services;
    }

    // ── Repositories ──────────────────────────────────────────────────────
    private static IServiceCollection AddIdentityRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // IPasswordHasher<User> from Microsoft.AspNetCore.Identity.Core — no full Identity stack needed
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        return services;
    }

    /// <summary>Runs EF Core migrations at startup if AutoMigrate is enabled.</summary>
    public static async Task MigrateIdentityDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbOptions = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>().Value;

        if (!dbOptions.AutoMigrate) return;

        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await context.Database.MigrateAsync();
    }
}
