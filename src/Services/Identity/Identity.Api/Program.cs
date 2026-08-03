using Identity.Application.DependencyInjection;
using Identity.Infrastructure.DependencyInjection;
using Identity.Infrastructure.Seeding;
using NovaStack.Infrastructure.DependencyInjection;
using NovaStack.Infrastructure.Logging;
using NovaStack.Infrastructure.Observability;
using NovaStack.Infrastructure.Persistence.Options;
using Scalar.AspNetCore;
using Serilog;

// ── Bootstrap logger ─────────────────────────────────────────────────────────
LoggingExtensions.BootstrapLogger();

try
{
    Log.Information("Starting Identity.Api...");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ───────────────────────────────────────────────────────────
    builder.Host.UseNovaStackSerilog();

    // ── OpenAPI / Swagger ─────────────────────────────────────────────────
    builder.Services.AddOpenApi();
    builder.Services.AddEndpointsApiExplorer();

    // ── Shared Infrastructure ─────────────────────────────────────────────
    builder.Services.AddNovaStackAuth(builder.Configuration);
    builder.Services.AddNovaStackCache(builder.Configuration);
    builder.Services.AddNovaStackHealthChecks(builder.Configuration);

    // ── OpenTelemetry ─────────────────────────────────────────────────────
    builder.Services.AddNovaStackObservability(
        serviceName: "Identity.Api",
        otlpEndpoint: builder.Configuration["Observability:OtlpEndpoint"]);

    // ── Application Layer ─────────────────────────────────────────────────
    builder.Services.AddIdentityApplication();

    // ── Infrastructure Layer ──────────────────────────────────────────────
    builder.Services.AddIdentityInfrastructure(builder.Configuration);

    // ── Authorization Policies ────────────────────────────────────────────
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
    });

    // ── CORS ──────────────────────────────────────────────────────────────
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });

    // ── Problem Details ───────────────────────────────────────────────────
    builder.Services.AddProblemDetails();

    var app = builder.Build();

    // ── Middleware ────────────────────────────────────────────────────────
    app.UseExceptionHandler();
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
    });
    app.MapPrometheusScrapingEndpoint();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseHttpsRedirection();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();

    // ── Endpoints ─────────────────────────────────────────────────────────
    app.MapIdentityEndpoints();
    app.MapHealthChecks("/health");

    // ── Auto-migrate + seed ───────────────────────────────────────────────
    await app.Services.MigrateIdentityDatabaseAsync();
    await IdentityDataSeeder.SeedAsync(app.Services);

    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var urls = string.Join(", ", app.Urls);
        Log.Information("Identity.Api is running on: {Urls}", urls);
        if (app.Environment.IsDevelopment())
        {
            var firstUrl = app.Urls.FirstOrDefault() ?? "http://localhost:5010";
            Log.Information("Scalar API reference: {Url}/scalar/v1", firstUrl.TrimEnd('/'));
            Log.Information("OIDC Discovery: {Url}/.well-known/openid-configuration", firstUrl.TrimEnd('/'));
            Log.Warning(
                "Default admin credentials — email: sysadmin@novastack.local / password: @superuser. " +
                "Change this password immediately in production!");
        }
    });

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Identity.Api terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

// Required for WebApplicationFactory in integration tests
public partial class Program { }
