using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NovaStack.Infrastructure.Authentication;
using NovaStack.Infrastructure.Caching;
using System.Text;

namespace NovaStack.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods to register all shared infrastructure services.
/// Call this from each service's composition root (Program.cs).
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>Registers JWT authentication with configuration from <see cref="JwtOptions"/>.</summary>
    public static IServiceCollection AddNovaStackAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        var jwtOptions = configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>() ?? new JwtOptions();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = jwtOptions.ValidateIssuer,
                    ValidateAudience = jwtOptions.ValidateAudience,
                    ValidateLifetime = jwtOptions.ValidateLifetime,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            string.IsNullOrWhiteSpace(jwtOptions.SecretKey)
                                ? "default_dev_key_please_change_me_32chars!"
                                : jwtOptions.SecretKey))
                };
            });

        services.AddAuthorization();

        // Token generation service (used by Identity.Application handlers)
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        return services;
    }

    /// <summary>Registers caching (in-memory or Redis) based on configuration.</summary>
    public static IServiceCollection AddNovaStackCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));

        var cacheOptions = configuration
            .GetSection(CacheOptions.SectionName)
            .Get<CacheOptions>() ?? new CacheOptions();

        if (cacheOptions.Provider == CacheProvider.Redis
            && !string.IsNullOrWhiteSpace(cacheOptions.RedisConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = cacheOptions.RedisConnectionString;
                options.InstanceName = cacheOptions.InstanceName;
            });
            services.AddSingleton<ICacheService>(sp =>
                new RedisCacheService(
                    sp.GetRequiredService<IDistributedCache>(),
                    cacheOptions));
        }
        else
        {
            services.AddMemoryCache();
            services.AddSingleton<ICacheService>(sp =>
                new InMemoryCacheService(
                    sp.GetRequiredService<IMemoryCache>(),
                    cacheOptions));
        }

        return services;
    }

    /// <summary>Registers health checks.</summary>
    public static IServiceCollection AddNovaStackHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddHealthChecks().Services;
}
