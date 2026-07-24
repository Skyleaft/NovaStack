<![CDATA[<div align="center">

# 🚀 NovaStack

**Enterprise-grade .NET 10 microservice boilerplate**  
*Vertical Slice Architecture · CQRS · Minimal API · Multi-DB · Multi-Broker · Docker-ready*

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-13.0-239120?style=flat-square&logo=csharp)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow?style=flat-square)](LICENSE)
[![Build](https://img.shields.io/badge/build-passing-brightgreen?style=flat-square)](#)

</div>

---

## ✨ Features

| Feature | Technology |
|---------|-----------|
| **Architecture** | Vertical Slice Architecture |
| **CQRS + Mediator** | MediatR 14 with pipeline behaviors |
| **API style** | ASP.NET Core Minimal API |
| **Database (multi)** | EF Core 10 — PostgreSQL or SQL Server (config-driven) |
| **Messaging (multi)** | MassTransit 9 — RabbitMQ or Kafka (config-driven) |
| **Outbox pattern** | Domain events → outbox table via EF Core interceptor |
| **Validation** | FluentValidation in MediatR pipeline |
| **Error handling** | Railway-oriented `Result<T>` — no exceptions for domain flow |
| **Caching** | In-memory or Redis (config-driven) |
| **Auth** | JWT Bearer |
| **Logging** | Serilog (console + rolling file, enriched) |
| **Observability** | OpenTelemetry traces + metrics + runtime instrumentation |
| **Testing** | xUnit · Moq · FluentAssertions · Testcontainers · NetArchTest |
| **Docker** | Multi-stage Dockerfiles, full docker-compose stack |

---

## 📁 Solution Structure

```
NovaStack/
├── src/
│   ├── BuildingBlocks/
│   │   ├── NovaStack.SharedKernel/        # Result<T>, Entity, Guard, Exceptions, ValueObjects
│   │   ├── NovaStack.Infrastructure/      # Auth, Cache, Logging, OTel, Persistence base, Messaging
│   │   └── NovaStack.Contracts/           # Integration events, API response shapes
│   │
│   └── Services/
│       ├── Product.Domain/                # Aggregate, ValueObjects, Domain Events, Repository interface
│       ├── Product.Application/           # CQRS vertical slices, pipeline behaviors, endpoint definitions
│       ├── Product.Infrastructure/        # EF Core DbContext, Repository impl, MassTransit wiring
│       └── Product.Api/                   # Minimal API host, composition root, Dockerfile
│
├── src/Workers/
│   ├── Product.Consumer/                  # MassTransit worker (RabbitMQ or Kafka)
│   └── Notification.Consumer/            # Notification dispatch worker
│
├── tests/
│   ├── UnitTests/                         # Handler unit tests (Moq + FluentAssertions)
│   ├── IntegrationTests/                  # API tests with real PostgreSQL (Testcontainers)
│   └── ArchitectureTests/                 # Dependency boundary tests (NetArchTest)
│
├── docker-compose.yml                     # Full local infrastructure stack
├── docker-compose.override.yml            # Dev environment overrides
├── Directory.Build.props                  # Shared MSBuild settings
└── NovaStack.sln
```

---

## 🏁 Quick Start

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### 1. Start infrastructure

```bash
docker-compose up postgres rabbitmq redis -d
```

### 2. Run the API

```bash
cd src/Services/Product.Api
dotnet run
```

API is available at `http://localhost:5000`  
OpenAPI spec: `http://localhost:5000/openapi/v1.json`

### 3. Run everything via Docker

```bash
docker-compose up --build
```

---

## ⚙️ Configuration

All behavior is driven by `appsettings.json`. No code changes required to switch providers.

### Database

```json
"Database": {
  "Provider": "PostgreSQL",              // or "SqlServer"
  "ConnectionString": "Host=localhost;...",
  "AutoMigrate": true
}
```

### Messaging

```json
"Messaging": {
  "Provider": "RabbitMQ",               // or "Kafka"
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest"
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "GroupId": "product-api-group"
  }
}
```

### Caching

```json
"Cache": {
  "Provider": "InMemory",               // or "Redis"
  "RedisConnectionString": "localhost:6379"
}
```

### JWT

```json
"Jwt": {
  "Issuer": "NovaStack",
  "Audience": "NovaStack.Clients",
  "SecretKey": "your-secret-key-min-32-chars",
  "ExpiryMinutes": 60
}
```

---

## 🔌 Product API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/v1/products` | Create a new product |
| `GET` | `/api/v1/products` | Paginated list (`?page=1&pageSize=10&search=`) |
| `GET` | `/api/v1/products/{id}` | Get product by ID |
| `PUT` | `/api/v1/products/{id}` | Update product |
| `DELETE` | `/api/v1/products/{id}` | Soft-delete (deactivate) |
| `GET` | `/health` | Health check |
| `GET` | `/openapi/v1.json` | OpenAPI spec (Development only) |

---

## 🧱 Architecture Deep Dive

### Vertical Slice Architecture

Each feature is self-contained in a folder. No cross-cutting shared layers — only shared *infrastructure*.

```
Features/
└── Products/
    ├── CreateProduct/
    │   ├── CreateProductCommand.cs       # Record: ICommand<Guid>
    │   ├── CreateProductCommandHandler.cs
    │   ├── CreateProductCommandValidator.cs
    │   └── CreateProductEndpoint.cs     # IEndpointDefinition
    ├── GetProductById/
    ├── GetProducts/
    ├── UpdateProduct/
    └── DeleteProduct/
```

### CQRS with Result Pattern

```csharp
// Command returns Result<Guid> — no exceptions for domain flow
public record CreateProductCommand(...) : ICommand<Guid>;

// Handler
public class CreateProductCommandHandler : ICommandHandler<CreateProductCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateProductCommand cmd, CancellationToken ct)
    {
        if (await _repo.ExistsByNameAsync(cmd.Name, ct))
            return Error.Conflict("Product.NameConflict", "Name already exists.");
        // ...
        return product.Id.Value;
    }
}
```

### Outbox Pattern

`DbContextBase.SaveChangesAsync` automatically captures domain events from any `IHasDomainEvents` entity and writes them to an `outbox_messages` table atomically in the same transaction.

### MediatR Pipeline

```
Request → LoggingBehavior → ValidationBehavior → Handler → Response
```

---

## 🧪 Running Tests

```bash
# Unit tests (no Docker required)
dotnet test tests/UnitTests

# Architecture boundary tests
dotnet test tests/ArchitectureTests

# Integration tests (requires Docker)
dotnet test tests/IntegrationTests
```

---

## 🗄️ Database Migrations

```bash
# Add a migration
dotnet ef migrations add InitialCreate \
  --project src/Services/Product.Infrastructure \
  --startup-project src/Services/Product.Api \
  --output-dir Persistence/Migrations

# Apply manually
dotnet ef database update \
  --project src/Services/Product.Infrastructure \
  --startup-project src/Services/Product.Api
```

> **Auto-migrate on startup** is enabled by default (`"AutoMigrate": true`). Disable in production if you prefer manual migration runs.

---

## 🐳 Docker Infrastructure

The `docker-compose.yml` brings up the full stack:

| Service | Port | Description |
|---------|------|-------------|
| PostgreSQL | `5432` | Primary database |
| SQL Server | `1433` | Alternative database |
| RabbitMQ | `5672` / `15672` | Message broker + management UI |
| Kafka | `9092` / `29092` | Event streaming |
| Redis | `6379` | Distributed cache |
| Product API | `5000` | REST API |
| Product Consumer | — | Background message consumer |
| Notification Consumer | — | Notification dispatch worker |

---

## 🔭 Observability

| Signal | Implementation |
|--------|---------------|
| **Structured logs** | Serilog → console + rolling file (`logs/novastack-YYYY-MM-DD.log`) |
| **Traces** | OpenTelemetry → ASP.NET Core + HTTP client instrumentation |
| **Metrics** | OpenTelemetry → ASP.NET Core + runtime metrics |
| **OTLP export** | Add `OpenTelemetry.Exporter.OpenTelemetryProtocol` and set `Observability:OtlpEndpoint` |

---

## 🗺️ Extending NovaStack

### Add a new service (e.g., Order service)

1. Create `Order.Domain`, `Order.Application`, `Order.Infrastructure`, `Order.Api` projects
2. Add project references following the same pattern
3. Register `IEndpointDefinition` implementations in the new API's `Program.cs`
4. Add to `docker-compose.yml`

### Add Scalar API UI

```bash
dotnet add src/Services/Product.Api package Scalar.AspNetCore
```

```csharp
// In Program.cs
app.MapScalarApiReference();
```

### Add OTLP export (Jaeger / Grafana Tempo)

```bash
dotnet add src/BuildingBlocks/NovaStack.Infrastructure package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

Set `Observability__OtlpEndpoint=http://jaeger:4317` in your environment.

---

## 📦 Key NuGet Packages

| Package | Version | Purpose |
|---------|---------|---------|
| `MediatR` | 14.x | CQRS mediator |
| `FluentValidation` | 12.x | Command/Query validation |
| `Microsoft.EntityFrameworkCore` | 10.x | ORM |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.x | PostgreSQL provider |
| `Microsoft.EntityFrameworkCore.SqlServer` | 10.x | SQL Server provider |
| `MassTransit` | 9.x | Message bus abstraction |
| `MassTransit.RabbitMQ` | 9.x | RabbitMQ transport |
| `MassTransit.Kafka` | 9.x | Kafka transport |
| `Serilog.AspNetCore` | 10.x | Structured logging |
| `OpenTelemetry.Extensions.Hosting` | 1.x | OTel SDK |
| `Testcontainers.PostgreSql` | 4.x | Integration test DB |
| `NetArchTest.Rules` | 1.x | Architecture enforcement |

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

---

<div align="center">

Built with ❤️ using **.NET 10** and **Vertical Slice Architecture**

</div>
]]>
