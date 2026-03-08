# dotnet-microservice-template

A production-ready .NET 9 microservice template based on a real layered architecture used across multiple microservices. Built on top of `Nitsol.Core` — a shared library providing base repository contracts, audit interfaces, and pagination models.

Clone this, rename the namespaces, and drop in your domain logic.

---

## Architecture

```
+---------------------------------------------------------------+
|                        API Gateway                            |
|                   (Ocelot / YARP / Nginx)                     |
+-------------------------------+-------------------------------+
                                |
                     +----------v----------+
                     |   ProductService    |
                     +----------+----------+
                                |
           +--------------------+--------------------+
           v                    v                    v
    +-------------+   +-----------------+   +-----------------+
    |   .API      |   |   .Services     |   |  .Repository    |
    | Controllers |   | Business logic  |   | EF Core, DbCtx  |
    +------+------+   +--------+--------+   | Factory         |
           |                   |            +--------+--------+
           +-------------------+--------------------+
                                |
                     +----------v----------+
                     |     .Models         |
                     | Entities, Enums,    |
                     | Repo Interfaces     |
                     +----------+----------+
                                |
                     +----------v----------+
                     |    Nitsol.Core      |
                     | IAuditableEntity    |
                     | IRepository<T>      |
                     | PagedEntities<T>    |
                     +---------------------+

Infrastructure:
  PostgreSQL       Redis       RabbitMQ
```

### Layer responsibilities

| Layer | Project | Responsibility |
|---|---|---|
| **Entry point** | `ProductService` | `Program.cs`, DI wiring, Dockerfile |
| **API** | `ProductService.API` | Controllers — receive HTTP, delegate to services |
| **Services** | `ProductService.Services` | Business logic, `BaseServices`, RabbitMQ pub/sub |
| **Repository** | `ProductService.Repository` | EF Core `DbContext`, `Repository<T>`, `RepositoryFactory`, configurations |
| **Models** | `ProductService.Models` | Domain entities, enums, RabbitMQ message models, repository interfaces |
| **Contracts** | `ProductService.Contracts` | DTOs with `BaseContract<TContract, TModel>`, AutoMapper profile |
| **Nitsol.Core** | `Nitsol.Core` | Shared base library — `IRepository<T>`, `IAuditableEntity`, `PagedEntities<T>` |

> In production, `Nitsol.Core` is published as a NuGet package consumed across all microservices. It is included here as a project reference to keep the template self-contained.

---

## Project Structure

```
dotnet-microservice-template/
+-- ProductService.sln
+-- docker-compose.yml
+-- .gitignore
+-- src/
    +-- Nitsol.Core/
    |   +-- Models/
    |   |   +-- IAuditableEntity.cs        # CreatedOn, ModifiedOn
    |   |   +-- PagedEntities.cs           # Generic pagination wrapper
    |   +-- Repositories/
    |       +-- IRepository.cs             # CRUD + paged read contracts
    |
    +-- ProductService.Models/
    |   +-- ProductModel.cs                # Entity implementing IAuditableEntity
    |   +-- ProductEnums.cs
    |   +-- RabbitMQModels.cs              # ProductMessage + OrderPlacedMessage
    |   +-- Repositories/
    |       +-- IProductRepository.cs      # Extends IRepository<ProductModel>
    |       +-- IRepositoryFactory.cs      # Unit-of-work factory interface
    |
    +-- ProductService.Repository/
    |   +-- Repository.cs                  # Abstract base — implements IRepository<T>
    |   +-- ProductRepository.cs           # Domain-specific queries
    |   +-- DatabaseContext.cs             # EF Core + auto-audit on SaveChanges
    |   +-- RepositoryFactory.cs           # Creates repos + Commit()
    |   +-- Configurations/
    |       +-- ProductConfiguration.cs    # Fluent API entity config
    |
    +-- ProductService.Services/
    |   +-- BaseServices.cs                # Abstract — holds DbContext + optional cache
    |   +-- ProductServices.cs             # Business logic extending BaseServices
    |   +-- ProductEventPublisher.cs       # Publishes domain events to RabbitMQ
    |   +-- ProductEventSubscriber.cs      # BackgroundService — consumes order events
    |
    +-- ProductService.Contracts/
    |   +-- BaseContract.cs                # ToContract / ToModel / ToContracts helpers
    |   +-- ProductContract.cs             # DTO extends BaseContract<...>
    |   +-- AutoMapperProfile.cs
    |
    +-- ProductService.API/
    |   +-- ProductAPI.cs                  # Controller — injects ProductServices + IMapper
    |
    +-- ProductService/                    # Entry point
        +-- Program.cs                     # DI wiring, middleware pipeline
        +-- appsettings.json
        +-- Dockerfile                     # Multi-stage build
```

---

## Key Patterns

### RepositoryFactory (Unit of Work)

All data access goes through `RepositoryFactory` inside a `using` block:

```csharp
using IRepositoryFactory factory = new RepositoryFactory(_Context);
var repo = factory.GetProductRepository();
var product = await repo.ReadAsync(id);
repo.Update(product.Update(updatedModel));
factory.Commit();
```

Atomic commit, clean disposal — consistent across every service.

### BaseServices

All service classes inherit `BaseServices`, which provides `_Context` and optional `_cacheService`:

```csharp
// Without cache
public ProductServices(DatabaseContext context, ...) : base(context) { }

// With Redis cache
public ProductServices(DatabaseContext context, ...,
    ICacheService cache, IOptions<CacheExpirationOptions> expiry)
    : base(context, cache, expiry) { }
```

### IAuditableEntity + Auto-audit

Any model implementing `IAuditableEntity` gets `CreatedOn`/`ModifiedOn` set automatically by `DatabaseContext.SaveChanges()` — no manual date assignment needed in service code:

```csharp
case EntityState.Added:
    entry.Entity.CreatedOn = DateTime.UtcNow;
    entry.Entity.ModifiedOn = DateTime.UtcNow;
    break;
case EntityState.Modified:
    entry.Entity.ModifiedOn = DateTime.UtcNow;
    break;
```

### BaseContract with AutoMapper

Contracts handle their own mapping — keeps controllers clean:

```csharp
// List response
return Ok(ProductContract.ToContracts(products, _mapper));

// Single response
return Ok(ProductContract.ToContract(product, _mapper));

// Body to model
await _services.CreateProduct(contract.ToModel(_mapper));
```

### Event-Driven Messaging (ECST)

ProductService both **publishes** and **consumes** events via RabbitMQ, demonstrating bidirectional Event-Carried State Transfer:

```
ProductService publishes:
  CreateProduct  -->  product.created  -->  exchange: service.events
  UpdateProduct  -->  product.updated
  DeleteProduct  -->  product.deleted

ProductService consumes:
  order.placed queue  -->  ProductEventSubscriber  -->  AdjustStockAsync
```

`ProductEventSubscriber` is a `BackgroundService` (`IHostedService`) that listens for `OrderPlaced` events and decrements stock accordingly — without any direct HTTP call to an OrderService.

### IHostedService — RabbitMQ lifecycle

`RabbitMQService` is registered with a three-line pattern so a single instance is shared between the DI container and the host:

```csharp
// One instance — resolved as three different things
builder.Services.AddSingleton<RabbitMQService>();
builder.Services.AddSingleton<IRabbitMQService>(sp => sp.GetRequiredService<RabbitMQService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<RabbitMQService>());
```

This ensures `StartAsync`/`StopAsync` is called by the host on app start/shutdown while the same connection is used everywhere.

---

## API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/v1/products` | Get all products |
| `GET` | `/api/v1/products/active` | Get active (non-deleted) products |
| `GET` | `/api/v1/products/paged?page=1&size=10` | DB-level paginated active products |
| `GET` | `/api/v1/products/{id}` | Get product by ID |
| `POST` | `/api/v1/products` | Create product |
| `PUT` | `/api/v1/products/{id}` | Update product |
| `DELETE` | `/api/v1/products/{id}` | Soft-delete product |
| `GET` | `/health` | Health check (DB connectivity) |

### Pagination response shape

```json
{
  "data": [...],
  "page": 1,
  "size": 10,
  "total": 47,
  "totalPages": 5
}
```

Pagination is applied at the database level via `IQueryable.Skip().Take()` — no full-table loads into memory.

---

## Design Decisions

These are intentional choices, not oversights. Documenting them here because senior reviewers often ask about them.

### CancellationToken — reads only, not writes

`CancellationToken` is threaded through `GetPagedProductsAsync` (and the repository layer below it) but **not** through write methods like `CreateProduct`, `UpdateProduct`, `DeleteProduct`.

**Why reads need it:** A paginated query over a large dataset holds a DB connection open for a meaningful duration. If the HTTP client disconnects mid-query (browser tab closed, timeout), `HttpContext.RequestAborted` fires. Without propagating that token to EF Core, the query continues executing and the connection is held unnecessarily. Under load, this compounds quickly.

**Why writes don't need it:** Write operations (`INSERT`, `UPDATE`) are short DB round-trips. Cancelling mid-write introduces a partial-state risk — the update could be applied without the subsequent cache invalidation or event publish completing. The cost of a wasted write is negligible; the cost of inconsistency is not. For writes, let them complete.

### Global exception handling via ProblemDetails

Controllers have no `try/catch`. Unhandled exceptions bubble up to `UseExceptionHandler()`, which returns a structured RFC 7807 `ProblemDetails` response:

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "An error occurred while processing your request.",
  "status": 500,
  "traceId": "00-abc123..."
}
```

This keeps every action method focused on the happy path. The tradeoff: per-request context (e.g. the product ID that caused the failure) is not in the error log. In production, structured logging middleware (`traceId` correlation + request parameters) fills that gap.

### Idempotency in event consumer — acknowledged but not implemented

`ProductEventSubscriber.HandleOrderPlacedAsync` does not guard against duplicate message delivery. RabbitMQ guarantees **at-least-once** delivery — after a consumer crash or broker restart, a message can be redelivered, which would decrement stock twice for the same order.

The production solution is to track processed `OrderId`s before acting (Redis `SADD` or a DB `ProcessedMessages` table), checked atomically before calling `AdjustStockAsync`. This is documented in the code comment inside `HandleOrderPlacedAsync`. It was deliberately left unimplemented here because it requires a storage dependency (Redis/DB table) that would be environment-specific and distract from the core pattern being demonstrated.

### Soft delete, not hard delete

`DeleteProduct` sets `IsActive = false` and `IsDeleted = true` rather than removing the row. This preserves referential integrity (order history still has valid product references) and supports audit/recovery scenarios. `GetAllActiveProducts` and `GetPagedProductsAsync` filter on `IsActive && !IsDeleted` at the query level.

---

## Quick Start

### Docker (recommended)

```bash
git clone https://github.com/YOUR_USERNAME/dotnet-microservice-template
cd dotnet-microservice-template

docker-compose up --build

# Swagger UI:   http://localhost:8080
# RabbitMQ UI:  http://localhost:15672  (guest / guest)
```

### Local development

```bash
# Requires: .NET 9 SDK, PostgreSQL, RabbitMQ, Redis

cd src/ProductService
dotnet run
```

### Running tests

```bash
dotnet test ProductService.sln
```

13 unit tests covering all service methods — CreateProduct, UpdateProduct, GetById, GetAll, GetAllActive, Delete (soft), GetPaged, AdjustStock. Uses xUnit + Moq + EF Core InMemory database.

---

## Adapting to a New Service

1. Rename `Product*` to your entity name across all projects
2. Update `ProductModel` fields and `ProductEnums`
3. Add domain-specific queries to `ProductRepository`
4. Update `IRepositoryFactory` and `RepositoryFactory` with your new repository
5. Add business logic to your service class extending `BaseServices`
6. Update `ProductContract` and `AutoMapperProfile`
7. Add EF migration:
   ```bash
   dotnet ef migrations add InitialCreate -p src/ProductService.Repository -s src/ProductService
   ```

---

## Tech Stack

| Technology | Version | Purpose |
|---|---|---|
| .NET / ASP.NET Core | 9.0 | Runtime + HTTP API |
| Entity Framework Core | 9.0.9 | ORM |
| Npgsql | 9.0.4 | PostgreSQL driver |
| RabbitMQ.Client | 6.8.1 | Message broker |
| AutoMapper | 15.0.1 | Object mapping |
| Swagger / Swashbuckle | 6.5.0 | API documentation |
| xUnit + Moq | — | Unit testing |
| Docker | — | Containerization |
