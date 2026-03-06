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
| **Services** | `ProductService.Services` | Business logic, `BaseServices`, RabbitMQ, event pub/sub |
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
    |   +-- RabbitMQModels.cs              # Event message contracts
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
    |   +-- ICacheService.cs               # Cache abstraction (Redis swap-ready)
    |   +-- CacheExpirationOptions.cs      # Short / Medium / Long TTL tiers
    |   +-- RabbitMQService.cs             # Pub/sub with auto-reconnect + retry
    |   +-- ProductEventPublisher.cs       # Publishes domain events
    |   +-- ProductEventSubscriber.cs      # Subscribes to events from other services
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

### Event-driven messaging

Every write publishes a domain event so other services react without direct HTTP coupling:

```
CreateProduct  -->  product.created  -->  exchange: service.events
UpdateProduct  -->  product.updated  -->  exchange: service.events
```

Other services bind their queues to these routing keys in their own subscriber.

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
# Requires: .NET 9 SDK, PostgreSQL, RabbitMQ

cd src/ProductService
dotnet run
```

---

## API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/v1/products` | Get all products |
| `GET` | `/api/v1/products/active` | Get all active products |
| `GET` | `/api/v1/products/{id}` | Get product by ID |
| `POST` | `/api/v1/products` | Create product |
| `PUT` | `/api/v1/products/{id}` | Update product |
| `GET` | `/health` | Health check |

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
| Docker | — | Containerization |
