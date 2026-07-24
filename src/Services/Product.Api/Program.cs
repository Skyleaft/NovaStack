using NovaStack.Infrastructure.DependencyInjection;
using NovaStack.Infrastructure.Logging;
using NovaStack.Infrastructure.Observability;
using Product.Application.DependencyInjection;
using Product.Infrastructure.DependencyInjection;
using Serilog;

// ── Bootstrap logger (captures startup errors) ───────────────────────────────
LoggingExtensions.BootstrapLogger();

try
{
    Log.Information("Starting Product.Api...");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ──────────────────────────────────────────────────────────────
    builder.Host.UseNovaStackSerilog();

    // ── OpenAPI / Swagger ────────────────────────────────────────────────────
    builder.Services.AddOpenApi();
    builder.Services.AddEndpointsApiExplorer();

    // ── Shared Infrastructure ────────────────────────────────────────────────
    builder.Services.AddNovaStackAuth(builder.Configuration);
    builder.Services.AddNovaStackCache(builder.Configuration);
    builder.Services.AddNovaStackHealthChecks(builder.Configuration);

    // ── OpenTelemetry ────────────────────────────────────────────────────────
    builder.Services.AddNovaStackObservability(
        serviceName: "Product.Api",
        otlpEndpoint: builder.Configuration["Observability:OtlpEndpoint"]);

    // ── Application Layer (MediatR, FluentValidation, Pipeline behaviors) ────
    builder.Services.AddProductApplication();
    builder.Services.AddNovaStackMappings(typeof(Product.Application.DependencyInjection.ApplicationExtensions).Assembly);

    // ── Infrastructure Layer (EF Core, Repos, MassTransit) ──────────────────
    builder.Services.AddProductInfrastructure(builder.Configuration);

    // ── CORS ─────────────────────────────────────────────────────────────────
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });

    // ── Problem Details ──────────────────────────────────────────────────────
    builder.Services.AddProblemDetails();

    var app = builder.Build();

    // ── Middleware ────────────────────────────────────────────────────────────
    app.UseExceptionHandler();
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
    });

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi(); // Access via /openapi/v1.json
        // Optional: add Scalar UI with: dotnet add package Scalar.AspNetCore
        // app.MapScalarApiReference();
    }

    app.UseHttpsRedirection();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();

    // ── Endpoints ─────────────────────────────────────────────────────────────
    // Scan Product.Application assembly for all IEndpointDefinition implementations
    app.MapProductEndpoints();

    // Health checks
    app.MapHealthChecks("/health");

    // ── Auto-migrate ─────────────────────────────────────────────────────────
    await app.Services.MigrateProductDatabaseAsync();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Product.Api terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

// Required for WebApplicationFactory in integration tests
public partial class Program { }
