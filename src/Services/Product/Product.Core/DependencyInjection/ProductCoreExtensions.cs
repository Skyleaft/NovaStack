using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NovaStack.Infrastructure.DependencyInjection;
using NovaStack.Infrastructure.Messaging.Options;
using NovaStack.Infrastructure.Persistence.Options;
using NovaStack.SharedKernel.Abstractions;
using Product.Core.Common.Abstractions;
using Product.Core.Common.Behaviors;
using Product.Core.Domain.Repositories;
using Product.Core.Persistence;
using Product.Core.Repositories;

namespace Product.Core.DependencyInjection;

/// <summary>
///     Registers all Product Core services: MediatR, FluentValidation, EF Core, Repositories, Unit of Work, and Messaging.
/// </summary>
public static class ProductCoreExtensions
{
    public static IServiceCollection AddProductCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var assembly = typeof(ProductCoreExtensions).Assembly;

        // ── MediatR with pipeline behaviors ──────────────────────────────────
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        // ── FluentValidation ──────────────────────────────────────────────────
        services.AddValidatorsFromAssembly(assembly);

        // ── Database & Persistence ───────────────────────────────────────────
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

        if (dbOptions.Provider == DatabaseProvider.MongoDB)
        {
            // Register native MongoDB client (singleton — connection-pooled by the driver)
            services.AddSingleton<IMongoClient>(_ =>
                new MongoClient(dbOptions.ConnectionString));

            // Register the MongoDB context for the Product service (scoped)
            services.AddScoped<ProductMongoDbContext>(sp =>
                new ProductMongoDbContext(
                    sp.GetRequiredService<IMongoClient>(),
                    dbOptions.DatabaseName));

            return services;
        }

        // ── EF Core (PostgreSQL / SQL Server) ──────────────────────────────
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
        services.AddScoped<IProductRepository>(sp =>
        {
            var mongoCtx = sp.GetService<ProductMongoDbContext>();
            if (mongoCtx is not null)
                return new MongoProductRepository(mongoCtx);

            return new ProductRepository(sp.GetRequiredService<ProductDbContext>());
        });

        return services;
    }

    // ── Messaging (Native Clients) ───────────────────────────────────────────
    private static IServiceCollection AddProductMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var messagingOptions = configuration
            .GetSection(MessagingOptions.SectionName)
            .Get<MessagingOptions>() ?? new MessagingOptions();

        services.Configure<MessagingOptions>(
            configuration.GetSection(MessagingOptions.SectionName));

        switch (messagingOptions.Provider)
        {
            case MessagingProvider.RabbitMQ:
                services.AddNativeRabbitMqEventBus();
                break;

            case MessagingProvider.Kafka:
                services.AddNativeKafkaEventBus();
                break;

            default:
                services.AddNativeRabbitMqEventBus();
                break;
        }

        return services;
    }

    /// <summary>
    ///     Scans the Product.Core assembly for all <see cref="IEndpointDefinition" /> implementations
    ///     and registers their routes.
    /// </summary>
    public static WebApplication MapProductEndpoints(this WebApplication app)
    {
        var endpointDefinitions = typeof(ProductCoreExtensions).Assembly
            .GetTypes()
            .Where(t => typeof(IEndpointDefinition).IsAssignableFrom(t)
                        && t is { IsInterface: false, IsAbstract: false })
            .Select(Activator.CreateInstance)
            .Cast<IEndpointDefinition>();

        foreach (var definition in endpointDefinitions)
            definition.DefineEndpoints(app);

        return app;
    }

    /// <summary>Runs EF Core migrations at startup if AutoMigrate is enabled. No-op for MongoDB.</summary>
    public static async Task MigrateProductDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbOptions = scope.ServiceProvider
            .GetRequiredService<IOptions<DatabaseOptions>>().Value;

        // MongoDB has no EF migrations — schema is schemaless by design.
        if (!dbOptions.AutoMigrate || dbOptions.Provider == DatabaseProvider.MongoDB) return;

        var context = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        await context.Database.MigrateAsync();
    }
}
