# 🚀 NovaStack LLM Context & Coding Specification

This document provides a token-efficient, high-density architectural and coding specification of **NovaStack**. It is designed to get LLM agents up to speed instantly with the project's structure, design patterns, coding conventions, and stack.

---

## 🛠️ Technology Stack

- **Runtime & Language**: .NET 10.0, C# 13.0
- **Architectural Style**: Vertical Slice Architecture (VSA)
- **CQRS & Mediator**: MediatR 14 (with logging and validation pipeline behaviors)
- **APIs**: ASP.NET Core Minimal APIs with custom route scanner (`IEndpointDefinition`)
- **Database (Multi-DB)**: EF Core 10 (PostgreSQL via Npgsql, SQL Server via MS SqlClient)
- **Query Optimization**: Dapper (for high-performance read-only queries)
- **Object Mapping**: Mapster
- **Validation**: FluentValidation 12
- **Error Handling**: Railway-oriented `Result<T>` and `Error` types (avoid domain exceptions)
- **Messaging (Multi-Broker)**: Native Clients for RabbitMQ and Confluent Kafka
- **Reliability**: Outbox Pattern (Domain Events captured automatically in `SaveChangesAsync`)
- **Testing**: xUnit, Moq, FluentAssertions, Testcontainers, NetArchTest (Architecture validation)

---

## 📂 Project Structure

```text
NovaStack/
├── src/
│   ├── BuildingBlocks/
│   │   ├── NovaStack.SharedKernel/        # Domain/Result bases: Result<T>, Entity<T>, Guard, exceptions
│   │   ├── NovaStack.Infrastructure/      # Shared wiring: Auth, Cache, Logging, OTel, Persistence, Messaging
│   │   └── NovaStack.Contracts/           # Inter-service schemas: Integration events, ApiResponse shapes
│   │
│   └── Services/
│       ├── Product.Domain/                # Aggregate roots, ValueObjects, Domain Events, Repositories interfaces
│       ├── Product.Application/           # CQRS vertical slices (Command/Query/Validator/Endpoint)
│       ├── Product.Infrastructure/        # EF DbContext, Repository impls, migrations, Dapper SqlConnectionFactory
│       └── Product.Api/                   # Composition root, Program.cs, config, Dockerfile
│
├── src/Workers/
│   ├── Product.Consumer/                  # Background service consumer (handles integration events)
│   └── Notification.Consumer/             # Background notification consumer
│
└── tests/
    ├── UnitTests/                         # Business logic tests (Moq + FluentAssertions)
    ├── IntegrationTests/                  # API tests with Docker PostgreSQL (Testcontainers)
    └── ArchitectureTests/                 # Structural constraint tests (NetArchTest)
```

---

## 🧱 Design Patterns & Coding Conventions

LLMs must follow these specific templates and patterns when adding new code.

### 1. Vertical Slice Architecture (VSA)
Features are placed inside self-contained folders in `Product.Application/Features/[FeatureName]/[CommandOrQuery]`. Each slice contains:
- `Command` or `Query` record
- `CommandHandler` or `QueryHandler` (`internal sealed`, primary constructors)
- `Validator` class (FluentValidation)
- `Endpoint` class implementing `IEndpointDefinition`

#### Command Vertical Slice Skeleton:
```csharp
// Features/Products/CreateProduct/CreateProductCommand.cs
using Product.Application.Common.Abstractions;

namespace Product.Application.Features.Products.CreateProduct;

public sealed record CreateProductCommand(
    string Name,
    string Description,
    decimal Price,
    string Currency,
    int StockQuantity
) : ICommand<Guid>;

// Features/Products/CreateProduct/CreateProductCommandHandler.cs
using NovaStack.SharedKernel.Abstractions;
using NovaStack.SharedKernel.Results;
using Product.Application.Common.Abstractions;
using Product.Domain.Repositories;
using Product.Domain.ValueObjects;
using DomainProduct = Product.Domain.Aggregates.Product;

namespace Product.Application.Features.Products.CreateProduct;

internal sealed class CreateProductCommandHandler(
    IProductRepository productRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateProductCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateProductCommand command, CancellationToken ct)
    {
        if (await productRepository.ExistsByNameAsync(command.Name, ct))
            return Error.Conflict("Product.NameConflict", $"Product '{command.Name}' already exists.");

        var product = DomainProduct.Create(
            ProductId.New(),
            command.Name,
            command.Description,
            Money.Create(command.Price, command.Currency),
            command.StockQuantity);

        await productRepository.AddAsync(product, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return product.Id.Value;
    }
}

// Features/Products/CreateProduct/CreateProductCommandValidator.cs
using FluentValidation;

namespace Product.Application.Features.Products.CreateProduct;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}

// Features/Products/CreateProduct/CreateProductEndpoint.cs
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NovaStack.Contracts.Responses;
using NovaStack.SharedKernel.Results;
using Product.Application.Common.Abstractions;

namespace Product.Application.Features.Products.CreateProduct;

public sealed class CreateProductEndpoint : IEndpointDefinition
{
    public void DefineEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/products", HandleAsync)
            .WithName("CreateProduct")
            .WithSummary("Create a new product")
            .WithTags("Products")
            .Produces<ApiResponse<Guid>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> HandleAsync(
        CreateProductCommand command,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? Results.Created($"/api/v1/products/{result.Value}", ApiResponse.Ok(result.Value, "Product created successfully."))
            : result.Error.ToHttpResult();
    }
}
```

### 2. Domain & Aggregates
Domain entities inherit from `Entity<TId>` (which implements `IEntity<TId>`, `IAggregateRoot<TId>`, and `IHasDomainEvents`).
- **Encapsulation**: Properties must be read-only from the outside (`{ get; private set; }`).
- **Factories**: Entities are instantiated via static factory methods (`Create(...)`) to enforce invariants.
- **Invariants**: Use the `Guard` class for validation. If violated, throw a `DomainException`.
- **Domain Events**: Raise events using `RaiseDomainEvent(...)`.

```csharp
using NovaStack.SharedKernel.Common;
using NovaStack.SharedKernel.Exceptions;

namespace Product.Domain.Aggregates;

public sealed class Product : Entity<ProductId>
{
    public string Name { get; private set; } = null!;
    public Money Price { get; private set; } = null!;
    
    private Product() : base() { } // Required for EF Core

    public static Product Create(ProductId id, string name, Money price)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.NotNull(price, nameof(price));

        var product = new Product { Id = id, Name = name, Price = price };
        product.RaiseDomainEvent(new ProductCreatedDomainEvent(id.Value, name));
        return product;
    }
}
```

### 3. Read Optimization with Dapper
Avoid EF Core for complex read-only queries (e.g. search, pagination, stock reports). Inject `ISqlConnectionFactory` and use raw SQL with Dapper:

```csharp
using Dapper;
using NovaStack.SharedKernel.Results;
using NovaStack.SharedKernel.Abstractions;
using Product.Application.Common.Abstractions;

namespace Product.Application.Features.Products.GetProductStockReport;

public record GetProductStockReportQuery() : IQuery<ProductStockReportResponse>;

internal sealed class GetProductStockReportQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    : IQueryHandler<GetProductStockReportQuery, ProductStockReportResponse>
{
    public async Task<Result<ProductStockReportResponse>> Handle(GetProductStockReportQuery query, CancellationToken ct)
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                COUNT(*) AS TotalProducts,
                COALESCE(SUM(stock_quantity), 0) AS TotalStock
            FROM products.products 
            WHERE is_active = true";

        var stats = await connection.QuerySingleOrDefaultAsync<ProductStockReportResponse>(sql);
        return stats ?? new ProductStockReportResponse(0, 0);
    }
}
```

### 4. Railway-Oriented Error Handling
Always return `Result<T>` or `Result` for application/domain control flow. Use `Error` types to signal business failures.
- Mapping to HTTP: Call `result.Error.ToHttpResult()` inside Endpoint handlers.
- Errors are mapped as follows in `ErrorExtensions`:
  - `ErrorType.NotFound` ➔ `404 Not Found`
  - `ErrorType.Validation` ➔ `400 Bad Request`
  - `ErrorType.Conflict` ➔ `409 Conflict`
  - `ErrorType.Unauthorized` ➔ `401 Unauthorized`
  - `ErrorType.Forbidden` ➔ `403 Forbidden`
  - `ErrorType.Failure` / default ➔ `500 Internal Server Error`

### 5. Outbox Pattern & Domain Events
- Any aggregate domain events raised during a transaction are intercepted during `SaveChangesAsync` inside `DbContextBase.cs`.
- They are serialized into the `outbox_messages` database table in the same atomic transaction.
- Background dispatchers process the outbox and forward messages downstream as integration events.

### 6. Integration Events & Messaging
Integration events reside in `NovaStack.Contracts/IntegrationEvents` and implement `IIntegrationEvent`.
- **Brokers**: Driven by RabbitMQ or Kafka depending on configuration (`appsettings.json` Messaging:Provider).
- **Publishing**: Inject and use `IEventBus`.
- **Consuming**: Create a consumer class inheriting from `IIntegrationEventHandler<TEvent>` and register it in the worker's `Program.cs`.

```csharp
// Consumer class
public sealed class ProductCreatedConsumer(ILogger<ProductCreatedConsumer> logger)
    : IIntegrationEventHandler<ProductCreatedIntegrationEvent>
{
    public async Task HandleAsync(ProductCreatedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        logger.LogInformation("Processing event: {EventId}", integrationEvent.EventId);
        await Task.CompletedTask;
    }
}

// Program.cs Registration
builder.Services.AddScoped<ProductCreatedConsumer>();
if (messagingOptions.Provider == MessagingProvider.RabbitMQ)
{
    builder.Services.AddRabbitMqConsumer<ProductCreatedIntegrationEvent, ProductCreatedConsumer>("product-created-queue");
}
```

---

## 🚫 LLM Guardrails & Code Standards

- **DO NOT** use horizontal layered structures (no controllers or separate services directory inside application layers).
- **DO NOT** throw domain/business exceptions in command/query handlers. Use `Result<T>` and `Error` variants.
- **DO** use primary constructors (`class CreateProductCommandHandler(IProductRepository productRepository...)`) in handlers and services.
- **DO** seal all command and query handlers (`internal sealed class`).
- **DO** use Dapper and raw SQL for high-performance read-only endpoints.
- **DO** use EF Core database interceptors/Outbox for transaction boundary synchronization.
- **DO NOT** register endpoints manually in `Program.cs`. Implementing `IEndpointDefinition` allows them to be scanned and registered automatically.
- **DO NOT** write unit tests with real databases. Use Mocking (`Moq`) for repositories, and verify execution flows.
- **DO** run `ArchitectureTests` to verify dependency directions (Domain ➔ Application ➔ Infrastructure).

---

## ⚡ Dev Operations

### Run Applications
- Start DB and services: `docker-compose up postgres rabbitmq redis -d`
- Run API: `cd src/Services/Product.Api && dotnet run`

### Run Migration Commands
```bash
# Add a migration
dotnet ef migrations add [MigrationName] --project src/Services/Product.Infrastructure --startup-project src/Services/Product.Api --output-dir Persistence/Migrations

# Update Database
dotnet ef database update --project src/Services/Product.Infrastructure --startup-project src/Services/Product.Api
```

### Run Tests
```bash
dotnet test tests/UnitTests
dotnet test tests/ArchitectureTests
dotnet test tests/IntegrationTests
```
