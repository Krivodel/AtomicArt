# Clean Architecture

## Core Principle

Dependencies point INWARD. Outer layers know about inner layers, never the reverse.

```
                  ┌──────────────┐
                  │   Domain     │
                  └──────▲───────┘
                         │
                  ┌──────┴───────┐
                  │ Application  │
                  └──────▲───────┘
                         │
                  ┌──────┴───────┐
                  │Infrastructure│
                  └──────▲───────┘
                         │
                  ┌──────┴───────┐
                  │     Api      │
                  └──────────────┘

Contracts is a boundary project referenced by Api, Application, Infrastructure, and Desktop.
Desktop references Contracts only.
```

`Program.cs` in the Api project is the API process composition root. It is allowed to reference every server layer, including Domain, to compose the object graph explicitly. This exception applies only to composition code in `Program.cs`; controllers, middleware, filters, and other HTTP adapter code must not use Domain types or behavior directly.

Desktop is a remote UI client. It must not reference Application, Domain, or Infrastructure. Desktop communicates with Api through HTTP clients using `ProjectName.Contracts` types.

Desktop may perform limited client-side business calculations only when the approved design explicitly records them as an architectural assumption for an operational reason, such as reducing server load or avoiding excessive API calls during interactive editing. Such logic must use only contract DTOs and local client state, must remain a preview/hint/projection, and must not become the source of truth for persistence, authorization, billing, server validation, paid execution, or irreversible business decisions. The authoritative rule remains in Domain/Application or is enforced by the server when correctness matters.

## The Dependency Rule

Source code dependencies point only inward. Nothing in an inner circle can reference anything in an outer circle — no functions, classes, variables, or any named entity.

The Dependency Rule is about architectural boundaries. It does not mean every class must depend only on interfaces.

Concrete dependencies inside the same layer/component are allowed when they are implementation details of that component. Prefer abstractions when crossing inward/outward boundaries, calling infrastructure/external systems, or depending on something expected to vary independently.

DTOs, requests, and responses shared by Api and Desktop live in `ProjectName.Contracts`. Contracts must not reference Domain. Application maps Domain entities to contract DTOs.

```csharp
// ✅ CORRECT — Application handler depends on Domain entity
public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, Result<OrderDto>>
{
    private readonly IOrderRepository _repo;

    public CreateOrderHandler(IOrderRepository repo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    public async Task<Result<OrderDto>> Handle(
        CreateOrderCommand request, CancellationToken ct)
    {
        Order order = Order.Create(
            new CustomerId(request.CustomerId),
            Quantity.Create(request.Quantity));
        await _repo.SaveAsync(order, ct).ConfigureAwait(false);

        OrderDto dto = OrderMappings.ToDto(order);
        return Result<OrderDto>.Success(dto);
    }
}

// ❌ WRONG — Domain entity depends on Infrastructure
public class Order
{
    public async Task Save(SqlConnection conn) { }  // Infrastructure in Domain!
}
```

## Handler Rules

Handlers are in the Application layer. They **coordinate** — they do NOT contain business logic.

### What Handlers Do

1. Receive a Command/Query
2. Load entities from repositories
3. Call domain methods on entities (business logic lives on the entity)
4. Persist changes via repositories
5. Map results to DTOs
6. Return the DTO

### What Handlers Do NOT Do

- Contain business rules (→ entity methods)
- Contain validation (→ FluentValidation validators)
- Contain SQL (→ repository)
- Contain HTTP logic (→ controller)
- Contain UI logic (→ ViewModel)
- Catch and swallow exceptions (→ pipeline behavior or middleware)

### Signs a Handler Has Too Much Logic

- `if/else` chains with business conditions → move to entity method
- Calculations (totals, discounts) → move to entity or domain service
- State validation ("can only cancel if Pending") → move to entity method
- More than ~20–30 lines in `Handle()` → likely doing too much

## Controller Rules

Controllers are HTTP adapters. They dispatch to MediatR and map responses.

```csharp
[ApiController]
[Route("api/v1/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrdersController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrderRequest request,
        CancellationToken ct)
    {
        CreateOrderCommand command = new(
            CustomerId: request.CustomerId,
            ProductId: request.ProductId,
            Quantity: request.Quantity);

        Result<OrderDto> result = await _mediator.Send(command, ct);

        return result.Match(
            success: dto => CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto),
            notFound: msg => NotFound(new ProblemDetails { Detail = msg }),
            error: msg => BadRequest(new ProblemDetails { Detail = msg }));
    }
}
```

### Controllers Do NOT

- Contain business logic of any kind
- Call repositories directly
- Validate input (validators do this)
- Transform data in controllers (handlers and application mapping classes do this)
- Catch exceptions (exception middleware does this)

## Interface Segregation

Small, focused interfaces. One interface per aggregate/concern.

```csharp
// ✅ CORRECT — focused
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct);
    Task SaveAsync(Order order, CancellationToken ct);
    Task UpdateAsync(Order order, CancellationToken ct);
}

// ❌ WRONG — god interface
public interface IRepository
{
    Task<T> GetByIdAsync<T>(Guid id);
    Task SaveAsync<T>(T entity);
    Task DeleteAsync<T>(Guid id);
}
```

## Result Pattern

Handlers return `Result<T>` for expected failures. Exceptions are for unexpected/infrastructure failures.

```csharp
// Expected failure → Result
if (product is null)
{
    return Result<OrderDto>.NotFound("Product not found");
}

// Unexpected failure → let it throw (caught by exception middleware)
```

## Contract Design

- Public methods accepting collections must not mutate them — treat as read-only.
- CQS: project-owned domain, service, repository, ViewModel, and application service methods with side effects return `void`/`Task`; methods returning a value have no side effects.
- Exception: command handlers may return an explicit outcome such as `Task<Result<T>>`, `Task<Result>`, a created object ID, an accepted response, or `Unit`/`Task` while changing state, because the value is the command outcome contract, not a query result. This exception applies only to command handlers and does not allow repositories or services to return success flags for side-effect methods.
- Boolean mode flags on public project-owned application, domain, service, and repository APIs are FORBIDDEN — use an `enum` or two named methods. Natural boolean values such as `SetEnabled(bool enabled)` are allowed.
- Framework-mandated signatures are exceptions: overrides, externally defined interface implementations, Avalonia attached property accessors, and `Dispose(bool disposing)`.

## Open/Closed Principle

Prefer extension over modification when adding a new independent variant, option, setting, strategy, provider, or cross-cutting concern. Modifying existing code is acceptable when fixing a bug, completing existing behavior, improving an internal algorithm, or changing logic that is naturally owned by that class. Code should be open for extension where the variation is expected and stable enough to justify an extension point.

### Detecting Violations

A design violates OCP when:
- Adding a new option requires editing a `switch` / `if-else` chain in an existing class
- A single class accumulates methods or properties that grow with each new variant
- Adding a feature touches 3+ existing files
- A single class holds configuration/behavior for N items that grow independently
- Repository interface grows with every new filter combination
- Cross-cutting concerns (caching, retry, logging) are mixed into business logic

### Example 1 — Settings System

```csharp
// ❌ FORBIDDEN — adding a setting requires modifying SettingsService + SettingsViewModel + AXAML
public class SettingsService
{
    public string Theme { get; set; } = "Light";
    public string Language { get; set; } = "en";
    public bool AutoSave { get; set; } = true;
    // Adding new setting = edit this class + ViewModel + View
}

// ✅ CORRECT — each setting is a self-contained class. Adding a new setting = 1 new class + register.

public interface ISettingDefinition
{
    string Key { get; }
    string DisplayName { get; }
    string Category { get; }
    Type ValueType { get; }
    object DefaultValue { get; }
    void Apply(object value);
}

public abstract class SettingDefinition<T> : ISettingDefinition
    where T : notnull
{
    public abstract string Key { get; }
    public abstract string DisplayName { get; }
    public abstract string Category { get; }
    public abstract T DefaultValue { get; }
    public Type ValueType => typeof(T);
    object ISettingDefinition.DefaultValue => DefaultValue;

    public abstract void Apply(object value);
}

public class ThemeSetting : SettingDefinition<string>
{
    public override string Key => "app.theme";
    public override string DisplayName => "Theme";
    public override string Category => "Appearance";
    public override string DefaultValue => "Light";

    public override void Apply(object value)
    {
        // apply theme
    }
}

public class AutoSaveSetting : SettingDefinition<bool>
{
    public override string Key => "editor.autoSave";
    public override string DisplayName => "Auto Save";
    public override string Category => "Editor";
    public override bool DefaultValue => true;

    public override void Apply(object value)
    {
        // apply auto-save
    }
}

// Consumer — never modified when a new setting is added
public class SettingsService : ISettingsService
{
    private readonly IReadOnlyList<ISettingDefinition> _definitions;
    private readonly Dictionary<string, object> _values;

    public SettingsService(IEnumerable<ISettingDefinition> definitions)
    {
        _definitions = definitions.ToList();
        _values = _definitions.ToDictionary(d => d.Key, d => d.DefaultValue);
    }

    public IReadOnlyList<ISettingDefinition> GetAll()
    {
        return _definitions;
    }

    public IReadOnlyList<ISettingDefinition> GetByCategory(string category)
    {
        return _definitions.Where(d => d.Category == category).ToList();
    }

    public void Set(string key, object value)
    {
        _values[key] = value;
        _definitions.First(d => d.Key == key).Apply(value);
    }
}

// DI — register all setting definitions from assembly
services.Scan(scan => scan
    .FromAssemblyOf<ThemeSetting>()
    .AddClasses(c => c.AssignableTo<ISettingDefinition>())
    .AsImplementedInterfaces()
    .WithSingletonLifetime());
```

### Example 2 — Decorator Chain for Cross-Cutting Concerns

Instead of one class that mixes business logic with caching, logging, retry — wrap thin decorators around a clean core. New concern = new decorator, zero changes to existing classes.

```csharp
// ❌ FORBIDDEN — repository mixes data access + caching + logging + retry
public class OrderRepository : IOrderRepository
{
    public async Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct)
    {
        _logger.LogInformation("Getting order {OrderId}", id);

        string cacheKey = $"order:{id.Value}";
        Order? cached = _cache.Get<Order>(cacheKey);

        if (cached is not null)
        {
            return cached;
        }

        int retries = 3;
        while (retries-- > 0)
        {
            try
            {
                using SqlConnection connection = _connectionFactory.CreateConnection();
                OrderRow? row = await connection
                    .QuerySingleOrDefaultAsync<OrderRow>(SelectByIdSql, new { Id = id.Value })
                    .ConfigureAwait(false);

                Order? order = row is null ? null : MapToEntity(row);
                _cache.Set(cacheKey, order, TimeSpan.FromMinutes(5));
                return order;
            }
            catch (SqlException) when (retries > 0)
            {
                await Task.Delay(200, ct).ConfigureAwait(false);
            }
        }

        return null;
    }
}

// ✅ CORRECT — each concern is a decorator. Core repository is pure data access.

// 1. Core — only data access
public class OrderRepository : IOrderRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public async Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct)
    {
        using SqlConnection connection = (SqlConnection)_connectionFactory.CreateConnection();
        OrderRow? row = await connection
            .QuerySingleOrDefaultAsync<OrderRow>(SelectByIdSql, new { Id = id.Value })
            .ConfigureAwait(false);

        return row is null ? null : MapToEntity(row);
    }
}

// 2. Caching decorator
public class CachedOrderRepository : IOrderRepository
{
    private readonly IOrderRepository _inner;
    private readonly IMemoryCache _cache;

    public CachedOrderRepository(IOrderRepository inner, IMemoryCache cache)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct)
    {
        string cacheKey = $"order:{id.Value}";

        if (_cache.TryGetValue(cacheKey, out Order? cached))
        {
            return cached;
        }

        Order? order = await _inner.GetByIdAsync(id, ct).ConfigureAwait(false);
        _cache.Set(cacheKey, order, TimeSpan.FromMinutes(5));
        return order;
    }
}

// 3. Retry decorator
public class RetryOrderRepository : IOrderRepository
{
    private readonly IOrderRepository _inner;

    public RetryOrderRepository(IOrderRepository inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public async Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct)
    {
        int retries = 3;

        while (true)
        {
            try
            {
                return await _inner.GetByIdAsync(id, ct).ConfigureAwait(false);
            }
            catch (SqlException) when (--retries > 0)
            {
                await Task.Delay(200, ct).ConfigureAwait(false);
            }
        }
    }
}

// DI registration — compose the chain: Retry → Cache → Core
services.AddScoped<OrderRepository>();
services.AddScoped<IOrderRepository>(sp =>
    new RetryOrderRepository(
        new CachedOrderRepository(
            sp.GetRequiredService<OrderRepository>(),
            sp.GetRequiredService<IMemoryCache>())));
```

Key difference from registry pattern: decorators **wrap** each other in a chain, each adding one behavior. The core class stays minimal, and adding/removing a concern means adding/removing a decorator without touching any existing class.

### Example 3 — Specification Pattern for Composable Queries

Instead of a growing list of repository methods (`GetByStatus`, `GetByCustomer`, `GetByDateRange`, `GetByStatusAndCustomerAndDateRange`...) — use composable specifications.

```csharp
// ❌ FORBIDDEN — method explosion: every new filter = new method or extra parameter
public interface IOrderReadRepository
{
    Task<IReadOnlyList<OrderListItemDto>> GetByStatusAsync(OrderStatus status, CancellationToken ct);
    Task<IReadOnlyList<OrderListItemDto>> GetByCustomerAsync(CustomerId customerId, CancellationToken ct);
    Task<IReadOnlyList<OrderListItemDto>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct);
    Task<IReadOnlyList<OrderListItemDto>> GetByStatusAndCustomerAsync(
        OrderStatus status, CustomerId customerId, CancellationToken ct);
    Task<IReadOnlyList<OrderListItemDto>> GetByStatusAndDateRangeAsync(
        OrderStatus status, DateTime from, DateTime to, CancellationToken ct);
    // combinatorial explosion — every combination = new method
}

// ✅ CORRECT — specifications compose, repository has ONE query method

public abstract class Specification<T>
{
    public abstract string ToSql();
    public abstract object ToParameters();

    public Specification<T> And(Specification<T> other)
    {
        return new AndSpecification<T>(this, other);
    }
}

public class OrderStatusSpecification : Specification<OrderListItemDto>
{
    private readonly OrderStatus _status;

    public OrderStatusSpecification(OrderStatus status)
    {
        _status = status;
    }

    public override string ToSql()
    {
        return "o.Status = @Status";
    }

    public override object ToParameters()
    {
        return new { Status = _status.ToString() };
    }
}

public class OrderDateRangeSpecification : Specification<OrderListItemDto>
{
    private readonly DateTime _from;
    private readonly DateTime _to;

    public OrderDateRangeSpecification(DateTime from, DateTime to)
    {
        _from = from;
        _to = to;
    }

    public override string ToSql()
    {
        return "o.CreatedAt >= @From AND o.CreatedAt <= @To";
    }

    public override object ToParameters()
    {
        return new { From = _from, To = _to };
    }
}

public class CustomerSpecification : Specification<OrderListItemDto>
{
    private readonly CustomerId _customerId;

    public CustomerSpecification(CustomerId customerId)
    {
        _customerId = customerId;
    }

    public override string ToSql()
    {
        return "o.CustomerId = @CustomerId";
    }

    public override object ToParameters()
    {
        return new { CustomerId = _customerId.Value };
    }
}

private class AndSpecification<T> : Specification<T>
{
    private readonly Specification<T> _left;
    private readonly Specification<T> _right;

    public AndSpecification(Specification<T> left, Specification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override string ToSql()
    {
        return $"({_left.ToSql()}) AND ({_right.ToSql()})";
    }

    public override object ToParameters()
    {
        // Merge anonymous objects into DynamicParameters
        DynamicParameters parameters = new();
        parameters.AddDynamicParams(_left.ToParameters());
        parameters.AddDynamicParams(_right.ToParameters());
        return parameters;
    }
}

// Repository — ONE method, never modified for new filters
public interface IOrderReadRepository
{
    Task<IReadOnlyList<OrderListItemDto>> QueryAsync(
        Specification<OrderListItemDto> spec, CancellationToken ct);
}

// Usage — compose freely
Specification<OrderListItemDto> spec = new OrderStatusSpecification(OrderStatus.Pending)
    .And(new OrderDateRangeSpecification(startDate, endDate))
    .And(new CustomerSpecification(customerId));

IReadOnlyList<OrderListItemDto> results = await _readRepo.QueryAsync(spec, ct);
```

Key difference: instead of N implementations chosen by a consumer, specifications are **composed together** by the caller to form a single query. New filter = 1 new specification class, no changes to the repository interface.

### Example 4 — Metadata-Driven Dynamic Forms

Instead of a separate AXAML view for every entity form — describe fields as metadata and render generically. New field = 1 new definition in the list, no AXAML changes.

```csharp
// ❌ FORBIDDEN — each entity has a bespoke form view, every field change = edit AXAML + ViewModel
// OrderFormView.axaml — hardcoded TextBox, ComboBox, DatePicker for each field
// CustomerFormView.axaml — another hardcoded form
// ProductFormView.axaml — yet another

// ✅ CORRECT — fields described as metadata, rendered by a generic form control

public enum FieldKind
{
    Text,
    Number,
    Date,
    Dropdown,
    Checkbox,
    Currency
}

public class FieldDefinition
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required FieldKind Kind { get; init; }
    public bool IsRequired { get; init; }
    public int DisplayOrder { get; init; }
    public string? Group { get; init; }
    public IReadOnlyList<string>? DropdownOptions { get; init; }
    public Func<object?, string?>? Validate { get; init; }
}

public interface IFormDefinition
{
    string EntityName { get; }
    IReadOnlyList<FieldDefinition> Fields { get; }
}

public class OrderFormDefinition : IFormDefinition
{
    public string EntityName => "Order";

    public IReadOnlyList<FieldDefinition> Fields =>
    [
        new FieldDefinition
        {
            Key = "customerName",
            Label = "Customer",
            Kind = FieldKind.Text,
            IsRequired = true,
            DisplayOrder = 10,
            Group = "General"
        },
        new FieldDefinition
        {
            Key = "quantity",
            Label = "Quantity",
            Kind = FieldKind.Number,
            IsRequired = true,
            DisplayOrder = 20,
            Group = "General",
            Validate = v => v is int n && n <= 0 ? "Must be positive" : null
        },
        new FieldDefinition
        {
            Key = "status",
            Label = "Status",
            Kind = FieldKind.Dropdown,
            DisplayOrder = 30,
            Group = "General",
            DropdownOptions = ["Pending", "Confirmed", "Shipped"]
        },
        new FieldDefinition
        {
            Key = "deliveryDate",
            Label = "Delivery Date",
            Kind = FieldKind.Date,
            DisplayOrder = 40,
            Group = "Shipping"
        }
    ];
}

// Generic FormViewModel — works for ANY entity, never modified
public partial class DynamicFormViewModel : ObservableValidator
{
    private readonly Dictionary<string, object?> _values = [];

    public IReadOnlyList<FieldDefinition> Fields { get; }
    public IReadOnlyList<string> Groups { get; }

    public DynamicFormViewModel(IFormDefinition definition)
    {
        Fields = definition.Fields.OrderBy(f => f.DisplayOrder).ToList();
        Groups = Fields.Select(f => f.Group ?? "General").Distinct().ToList();

        foreach (FieldDefinition field in Fields)
        {
            _values[field.Key] = null;
        }
    }

    public object? GetValue(string key)
    {
        return _values.GetValueOrDefault(key);
    }

    public void SetValue(string key, object? value)
    {
        _values[key] = value;
        OnPropertyChanged(key);
    }
}
```

Key difference: instead of N implementations running **side by side**, this is a **data-driven** approach — the shape of the UI is defined by data structures, not code. New field = add an entry to a list, no new class, no AXAML change.

### Example 5 — Convention-Based Auto-Registration

Instead of writing explicit DI registration, explicit mapping, or explicit routing for every new class — use conventions that discover and wire things automatically. New handler/mapper/endpoint = just create the class, conventions find it.

```csharp
// ❌ FORBIDDEN — every new feature requires manual registration in 3 places
// DependencyInjection.cs:
services.AddScoped<IOrderRepository, OrderRepository>();
services.AddScoped<ICustomerRepository, CustomerRepository>();
services.AddScoped<IProductRepository, ProductRepository>();
services.AddScoped<IInvoiceRepository, InvoiceRepository>();
// ... grows with every new repository

// MappingConfig.cs:
cfg.CreateMap<OrderRow, Order>();
cfg.CreateMap<CustomerRow, Customer>();
cfg.CreateMap<ProductRow, Product>();
// ... grows with every new entity

// ✅ CORRECT — conventions handle discovery, new class = zero registration edits

// Convention: every IXxxRepository ↔ XxxRepository pair auto-registers as Scoped
public static IServiceCollection AddRepositoriesByConvention(
    this IServiceCollection services, Assembly assembly)
{
    IEnumerable<Type> repositoryTypes = assembly.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract)
        .Where(t => t.GetInterfaces()
            .Any(i => i.Name.EndsWith("Repository", StringComparison.Ordinal)));

    foreach (Type implementationType in repositoryTypes)
    {
        Type? interfaceType = implementationType.GetInterfaces()
            .FirstOrDefault(i => i.Name == $"I{implementationType.Name}");

        if (interfaceType is not null)
        {
            services.AddScoped(interfaceType, implementationType);
        }
    }

    return services;
}

// Convention: every IXxxApiClient ↔ XxxApiClient pair auto-registers via HttpClientFactory
public static IServiceCollection AddApiClientsByConvention(
    this IServiceCollection services, Assembly assembly, IConfiguration configuration)
{
    IEnumerable<Type> clientTypes = assembly.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract)
        .Where(t => t.GetInterfaces()
            .Any(i => i.Name.EndsWith("ApiClient", StringComparison.Ordinal)));

    foreach (Type implementationType in clientTypes)
    {
        Type? interfaceType = implementationType.GetInterfaces()
            .FirstOrDefault(i => i.Name == $"I{implementationType.Name}");

        if (interfaceType is not null)
        {
            services.AddHttpClient(interfaceType, implementationType, client =>
            {
                string apiBaseUrl = configuration["ApiBaseUrl"]
                    ?? throw new InvalidOperationException("Configuration value 'ApiBaseUrl' is missing");

                client.BaseAddress = new Uri(apiBaseUrl);
            });
        }
    }

    return services;
}

// Composition root — stable forever
services.AddRepositoriesByConvention(typeof(OrderRepository).Assembly);
services.AddApiClientsByConvention(typeof(OrderApiClient).Assembly, configuration);
```

Key difference: instead of defining **what** each variant does (strategy pattern), conventions define **how classes are discovered and wired**. The registration code never changes — you just follow the naming convention and the class is automatically part of the system.

### Extensibility Rules

**Registry pattern (Example 1):**
1. **New independent variant = new class** — avoid modifying the consumer when the variation is expected and has a stable extension point
2. **Interface per concept** — `ISettingDefinition`, `IReportExporter`, `INotificationChannel`
3. **Self-describing implementations** — each class carries its own metadata (Key, DisplayName, etc.)
4. **Collection injection** — consumer receives `IEnumerable<IXxx>`, builds a registry
5. **Auto-discovery via DI** — scan assemblies, do not register each implementation manually when possible
6. **No switch/if-else chains on type or kind** — replace with dictionary lookup or polymorphism
7. **Metadata on the implementation** — display names, categories, sort order live on the class, not in a central list

**Decorator pattern (Example 2):**
8. **One concern per decorator** — caching, retry, logging are separate wrappers around a clean core
9. **Same interface** — decorator and core implement the same interface, chain composes via DI
10. **Core class has zero cross-cutting logic** — only the primary responsibility

**Specification pattern (Example 3):**
11. **New filter = new specification class** — repository interface stays stable
12. **Compose, don't multiply** — specifications combine via `.And()` / `.Or()`, no combinatorial method explosion

**Data-driven pattern (Example 4):**
13. **Describe, don't hardcode** — when UI structure varies per entity, define it as data (field definitions), not as bespoke AXAML per entity
14. **Generic renderer** — one ViewModel/View handles all shapes defined by metadata

**Convention pattern (Example 5):**
15. **Convention over registration** — if classes follow a naming pattern, discover and wire them automatically
16. **Composition root is stable** — adding a new class that follows the convention requires zero registration changes

## Rules Summary

1. **Dependencies point inward** — outer depends on inner, never reverse
2. **Domain is the center** — no outward project/layer references
3. **Handlers coordinate** — no business rules
4. **Business logic on entities** — entity methods enforce invariants
5. **Controllers are thin** — parse → MediatR → map response
6. **Validation is separate** — FluentValidation in pipeline
7. **Focused interfaces** — one per aggregate/concern
8. **Result pattern for expected failures** — exceptions for unexpected
9. **No layer skipping** — Controller → MediatR → Handler → repository/port when persistence or external access is needed
10. **Boolean mode flags on project-owned public APIs — FORBIDDEN**; natural boolean values and framework-mandated signatures are allowed
11. **Open/Closed Principle** — prefer new implementations for expected independent variants; modifying existing code is acceptable for bug fixes, owned behavior, and internal algorithms
