using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NovaStack.Infrastructure.Messaging.Options;
using NovaStack.Infrastructure.Persistence;
using NovaStack.Infrastructure.Persistence.Options;
using NovaStack.SharedKernel.Abstractions;
using Product.Domain.Repositories;
using Product.Infrastructure.Persistence;
using Product.Infrastructure.Repositories;

namespace Product.Infrastructure.DependencyInjection;

/// <summary>
/// Registers all Product infrastructure services:
/// EF Core, Repository, Unit of Work, and MassTransit messaging.
/// </summary>
public static class InfrastructureExtensions
{
    public static IServiceCollection AddProductInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddProductDatabase(configuration)
            .AddProductMessaging(configuration)
            .AddProductRepositories();

        return services;
    }

    // ── Database ──────────────────────────────────────────────────────────
    private static IServiceCollection AddProductDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbOptions = configuration
            .GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>() ?? new DatabaseOptions();

        services.Configure<DatabaseOptions>(
            configuration.GetSection(DatabaseOptions.SectionName));

        services.AddDbContext<ProductDbContext>(options =>
        {
            switch (dbOptions.Provider)
            {
                case DatabaseProvider.PostgreSQL:
                    options.UseNpgsql(dbOptions.ConnectionString, npgsql =>
                    {
                        npgsql.MigrationsHistoryTable("__ef_migrations_history", "products");
                        npgsql.EnableRetryOnFailure(3);
                    });
                    break;

                case DatabaseProvider.SqlServer:
                    options.UseSqlServer(dbOptions.ConnectionString, sql =>
                    {
                        sql.MigrationsHistoryTable("__ef_migrations_history", "products");
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

        // Register Unit of Work
        services.AddScoped<IUnitOfWork>(sp =>
            sp.GetRequiredService<ProductDbContext>());

        // Register SQL Connection Factory for Dapper queries
        services.AddScoped<ISqlConnectionFactory, SqlConnectionFactory>();

        return services;
    }

    // ── Repositories ──────────────────────────────────────────────────────
    private static IServiceCollection AddProductRepositories(
        this IServiceCollection services)
    {
        services.AddScoped<IProductRepository, ProductRepository>();
        return services;
    }

    // ── Messaging (MassTransit) ───────────────────────────────────────────
    private static IServiceCollection AddProductMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var messagingOptions = configuration
            .GetSection(MessagingOptions.SectionName)
            .Get<MessagingOptions>() ?? new MessagingOptions();

        services.Configure<MessagingOptions>(
            configuration.GetSection(MessagingOptions.SectionName));

        services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();

            switch (messagingOptions.Provider)
            {
                case MessagingProvider.RabbitMQ:
                    bus.UsingRabbitMq((ctx, cfg) =>
                    {
                        var rabbit = messagingOptions.RabbitMQ;
                        cfg.Host(rabbit.Host, rabbit.Port, rabbit.VirtualHost, h =>
                        {
                            h.Username(rabbit.Username);
                            h.Password(rabbit.Password);
                        });
                        cfg.ConfigureEndpoints(ctx);
                    });
                    break;

                case MessagingProvider.Kafka:
                    bus.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
                    bus.AddRider(rider =>
                    {
                        rider.UsingKafka((ctx, k) =>
                        {
                            k.Host(messagingOptions.Kafka.BootstrapServers);
                        });
                    });
                    break;

                default:
                    bus.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
                    break;
            }
        });

        return services;
    }

    /// <summary>Runs EF Core migrations at startup if AutoMigrate is enabled.</summary>
    public static async Task MigrateProductDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbOptions = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>().Value;

        if (!dbOptions.AutoMigrate) return;

        var context = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        await context.Database.MigrateAsync();
    }
}
