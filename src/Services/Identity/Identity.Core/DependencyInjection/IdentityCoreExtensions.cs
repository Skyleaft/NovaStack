using FluentValidation;
using Identity.Core.Common.Abstractions;
using Identity.Core.Common.Behaviors;
using Identity.Core.Domain.Aggregates;
using Identity.Core.Domain.Repositories;
using Identity.Core.Persistence;
using Identity.Core.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NovaStack.Infrastructure.Persistence.Options;
using NovaStack.SharedKernel.Abstractions;

namespace Identity.Core.DependencyInjection;

/// <summary>
/// Registers all Identity Core services: MediatR, FluentValidation, EF Core, Repositories, Unit of Work, Password Hasher, and Dapper.
/// </summary>
public static class IdentityCoreExtensions
{
    public static IServiceCollection AddIdentityCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var assembly = typeof(IdentityCoreExtensions).Assembly;

        // ── MediatR with pipeline behaviors ──────────────────────────────────
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        // ── FluentValidation ──────────────────────────────────────────────────
        services.AddValidatorsFromAssembly(assembly);

        // ── Database & Repositories ───────────────────────────────────────────
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

    /// <summary>
    /// Scans the Identity.Core assembly for all <see cref="IEndpointDefinition"/>
    /// implementations and registers their routes.
    /// </summary>
    public static WebApplication MapIdentityEndpoints(this WebApplication app)
    {
        var endpointDefinitions = typeof(IdentityCoreExtensions).Assembly
            .GetTypes()
            .Where(t => typeof(IEndpointDefinition).IsAssignableFrom(t)
                        && t is { IsInterface: false, IsAbstract: false })
            .Select(Activator.CreateInstance)
            .Cast<IEndpointDefinition>();

        foreach (var definition in endpointDefinitions)
            definition.DefineEndpoints(app);

        return app;
    }

    /// <summary>Runs EF Core migrations at startup if AutoMigrate is enabled.</summary>
    public static async Task MigrateIdentityDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbOptions = scope.ServiceProvider
            .GetRequiredService<IOptions<DatabaseOptions>>().Value;

        if (!dbOptions.AutoMigrate) return;

        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await context.Database.MigrateAsync();
    }
}
