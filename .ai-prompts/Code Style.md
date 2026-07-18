# Code Style

All rules in this document are HARD rules. Violations are automatic REJECT.

---

## Naming Conventions

Follow Microsoft C# Coding Conventions exactly.

| Element | Convention | Example |
|---------|-----------|---------|
| Namespace | PascalCase, match folder | `ProjectName.Application.Features.Orders` |
| Class / Struct / Record | PascalCase, noun | `OrderRepository`, `CreateOrderHandler` |
| Interface | `I` + PascalCase | `IOrderRepository`, `IDateTimeProvider` |
| Method | PascalCase, verb | `GetByIdAsync`, `CalculateTotal` |
| Async method | PascalCase + `Async` suffix | `GetByIdAsync`, `SaveAsync` |
| Property | PascalCase | `TotalAmount`, `CreatedAt` |
| Event | PascalCase | `OrderCreated`, `PropertyChanged` |
| Constant (field or local) | PascalCase | `MaxRetryCount`, `DefaultPageSize` |
| Private instance field | `_camelCase` | `_orderRepository`, `_logger` |
| Private/internal static field | PascalCase | `private static IWorkerQueue WorkerQueue;` |
| Thread-static field | `t_` prefix | `[ThreadStatic] private static TimeSpan t_timeSpan;` |
| Parameter | camelCase | `orderId`, `cancellationToken` |
| Local variable | camelCase | `order`, `totalAmount` |
| Enum (non-flags) | Singular noun | `enum OrderStatus { Pending, Shipped }` |
| Enum (flags) | Plural noun | `[Flags] enum Permissions { Read = 1, Write = 2 }` |
| Attribute type | Ends with `Attribute` | `SerializableAttribute` |
| `EventArgs` subclass | Own file, named after class | `UserJoinedEventArgs.cs` |
| Generic type param | `T` prefix, descriptive | `TSession`, `TInput`, `TOutput` |

### Naming Rules

- No Hungarian notation, no abbreviations (except: `Id`, `Ui`, `Vm`).
- Prefer clarity over brevity — meaningful, descriptive names.
- Avoid single-letter names except for loop counters (`i`, `j`, `k`).
- Identifiers must not contain two consecutive underscores (`__`) — reserved for compiler-generated identifiers.
- One class / interface / enum per file. File name == type name.
- Primary constructor parameters: PascalCase for `record` types, camelCase for `class` and `struct`.
- `EventArgs` subclass — always a dedicated file. Never nested inside another class.
- All naming conventions for `public` apply equally to `protected` and `protected internal`.

---

## Type and Syntax Rules

### Explicit Types — No `var`

NEVER use `var`. Always declare the explicit type.

```csharp
// ✅ CORRECT
Order order = await _repo.GetByIdAsync(id, ct).ConfigureAwait(false);
List<OrderDto> items = result.ToList();
IReadOnlyList<string> names = GetNames();

// ❌ FORBIDDEN
var order = await _repo.GetByIdAsync(id, ct);
var items = result.ToList();
```

### Target-Typed `new()`

Use `new()` ONLY when the concrete type is explicitly stated on the left-hand side of the SAME statement:

```csharp
// ✅ CORRECT — left-hand side has the concrete type
MyService myService = new();
object lockObj = new();

// ✅ CORRECT — full form required in all other cases
_myService = new MyService();                   // assignment separate from declaration
IMyService myService = new MyService();         // left-hand side is interface
return new MyService();                         // return statement
Configure(new MyService());                     // argument expression
```

### Collection Expressions

Use `[]` for collection/array initialization ONLY when the concrete type is explicitly stated on the left-hand side of the SAME statement. When the type is not visible on the left (field assignment, return, argument, etc.) — use full `new` with the explicit type:

```csharp
// ✅ CORRECT — concrete type on the left-hand side
List<string> items = [];
Dictionary<string, int> cache = [];
ObservableCollection<OrderDto> orders = [];
string[] vowels = ["a", "e", "i", "o", "u"];
List<IMyService> list = [new MyService()];

// ✅ CORRECT — full form required when type is not on the left
_cache = new Dictionary<string, int>();          // field assignment separate from declaration
_items = new List<string>();                     // field assignment separate from declaration
return new List<OrderDto>();                     // return statement
Configure(new List<string>());                   // argument expression

// ❌ FORBIDDEN — no type on the left, unreadable
_cache = [];
_items = [];

// ❌ FORBIDDEN — new() for collections
List<string> items = new();
Dictionary<string, int> cache = new();
```

### Expression-Bodied Members

Use `=>` for single-expression **property getters and accessors**. FORBIDDEN for methods and constructors.

#### Read-only properties — short form

When a property has NO setter, use the concise expression-bodied syntax. Never wrap a lone `get =>` in braces.
```csharp
// ✅ CORRECT — read-only, short form
public string Name => _name;
public byte[] Value => (byte[])_value.Clone();
public decimal Total => _items.Sum(x => x.Amount);

// ❌ FORBIDDEN — unnecessary braces around a lone getter
public string Name
{
    get => _name;
}
```

#### Properties with getter AND setter — expression-bodied accessors

When a property has both `get` and `set`, each accessor goes on its own line with `=>`.
```csharp
// ✅ CORRECT
public int Value
{
    get => _value;
    set => _value = value;
}

public string Status
{
    get => _status;
    private set => _status = value;
}
```

#### Methods and constructors — block body only
```csharp
// ❌ FORBIDDEN — methods
void MyMethod() => DoSomething();

// ❌ FORBIDDEN — constructors
public MyClass(int id) => _id = id;

// ✅ CORRECT
void MyMethod()
{
    DoSomething();
}

public MyClass(int id)
{
    _id = id;
}
```

### Built-In Type Keywords

Always use language keywords, not framework names: `string` not `String`, `int` not `Int32`, `long` not `Int64`, `object` not `Object`. Includes `nint` and `nuint`.

Prefer `int` over unsigned types (`uint`, `ushort`, `ulong`) unless unsigned semantics are explicitly required.

### Null Handling

- Use `?.`, `??`, `??=` where appropriate.
- Prefer `is null` / `is not null` over `== null` / `!= null`.
- Prefer `string.IsNullOrWhiteSpace` over manual null/empty checks.
- Enable nullable reference types project-wide. Annotate all reference types explicitly.
- NEVER use `!` (null-forgiving operator) — fix the root cause.

### Pattern Matching

Prefer pattern matching over type checks and null checks:

```csharp
if (result is { IsSuccess: true, Value: OrderDto order })
{
    return Ok(order);
}

if (order is null)
{
    return Result<OrderDto>.NotFound("Order not found");
}
```

### Miscellaneous

- No magic numbers — use named constants or resource keys.
- Never add XML documentation (`/// <summary>` comments) — FORBIDDEN.
- Avoid comments by default. Comments are allowed only when they explain a critical, complex, non-obvious, or risky decision that cannot be made clear through naming or structure alone.
- Comments must be short and explain WHY the code is shaped this way, not WHAT each line does.
- No `#region` — FORBIDDEN. If you need regions, your class is too big.
- Don't add namespaces `System`, `System.Collections.Generic`, `System.Threading.Tasks`, `System.Linq` — they are configured as global usings.
- Use `required` properties over constructor parameters when the goal is solely to force initialization.
- Use object initializers to simplify creation when setting multiple properties.

---

## Control Flow Style

Always use braces `{}` for `if` / `for` / `foreach` / `while`, even single-line. Body on the NEXT line — never same line as condition.

```csharp
// ❌ FORBIDDEN
if (a1) return;
if (a2) MyMethod();

// ✅ CORRECT
if (a1)
{
    return;
}

if (a2)
{
    MyMethod();
}
```

---

## Formatting and Layout

- Four spaces for indentation. Never tab characters.
- Allman brace style: opening and closing brace each on its own line.
- One statement per line. One declaration per line.
- At least one blank line between method definitions. NO blank lines between consecutive properties or between consecutive fields.
- ONE blank line between groups of different member kinds (properties, events, methods, etc.) — in classes, structs, records, and interfaces alike.
- Continuation lines indented one tab stop (four spaces).
- Line breaks before binary operators in multi-line statements.
- Break long fully qualified names after a dot (`.`).
- Parentheses to make compound expressions explicit: `if ((startX > endX) && (startX > previousX))`.

### Blank Lines Inside Method Bodies

Separate logical blocks within a method body with ONE blank line. A "logical block" is any group of related statements that forms a single step — variable declarations, a control flow statement (`if`, `foreach`, `for`, `while`, `switch`), a `try`/`catch`, a `return`, etc. The goal: each block reads as its own paragraph.

```csharp
// ❌ FORBIDDEN — no separation between logical blocks
public ThemeService(IEnumerable<IThemeProvider> providers)
{
    List<ThemeConfiguration> configurations = providers
        .Select(p => p.Configuration)
        .ToList();
    if (configurations.Count == 0)
    {
        // ...
    }

    _themesById = new Dictionary<string, ThemeConfiguration>();
    foreach (ThemeConfiguration config in configurations)
    {
        // ...
    }
}

// ✅ CORRECT — blank line before each new logical block
public ThemeService(IEnumerable<IThemeProvider> providers)
{
    List<ThemeConfiguration> configurations = providers
        .Select(p => p.Configuration)
        .ToList();

    if (configurations.Count == 0)
    {
        // ...
    }

    _themesById = new Dictionary<string, ThemeConfiguration>();

    foreach (ThemeConfiguration config in configurations)
    {
        // ...
    }
}
```

Do NOT add blank lines inside a single logical block (e.g. between consecutive related assignments or between guard clauses that form one validation group).

---

## Member Ordering

Arrange type members in a consistent, predictable order. Every class, struct, and record must follow this layout.

The class is split into three zones: **data** (state) at the top, **constructors** in the middle, **behavior** (logic) at the bottom. Within data and behavior zones, members are ordered by accessibility.

### 1. Data Zone — All State First (top to bottom)

Ordered by accessibility (`public` → `internal` → `protected internal` → `protected` → `private protected` → `private`). Within each accessibility level:

| Order | Member Kind |
|-------|------------|
| 1 | Constants (`const`) |
| 2 | Static properties |
| 3 | Instance properties |
| 4 | Static fields |
| 5 | Instance fields |
| 6 | Events |

### 2. Constructors — Immediately After All Data

All constructors go here, regardless of accessibility. Ordered by accessibility (`public` → `internal` → `protected` → `private`), then by parameter count (fewer parameters first). Static constructors come before instance constructors.

### 3. Behavior Zone — All Logic After (top to bottom)

Same accessibility order. Within each accessibility level:

| Order | Member Kind |
|-------|------------|
| 1 | Static factory methods (`Create`, `From`) |
| 2 | Finalizers / Destructors |
| 3 | Indexers |
| 4 | Methods |
| 5 | Nested types |

### 4. Additional Rules

- `static` members come before instance members within the same kind.
- Separate each accessibility group with a blank line. Within an accessibility group, separate different member kinds with a blank line.
- Interface implementations are grouped together immediately after the last public method, in the same order as the interfaces appear in the class declaration.
- `IDisposable.Dispose` / `Dispose(bool)` always goes at the very end of the public methods, right before the next accessibility group or nested types.
- `readonly` fields come before mutable fields within the same accessibility group.
- Event handler methods (`On…`) are placed at the end of the methods in their accessibility group, before `Dispose` (if applicable).

```csharp
public class OrderService : IOrderService, IDisposable
{
    // ── Data: public ──
    public const string ServiceName = "OrderService";

    public string Name => nameof(OrderService);
    public bool IsReady { get; private set; }

    public event EventHandler? OrderProcessed;

    // ── Data: private ──
    private const int MaxRetryCount = 3;
    private const int DefaultPageSize = 25;

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<OrderService> _logger;
    private bool _disposed;

    // ── Constructors ──
    public OrderService(IOrderRepository orderRepository, ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ── Behavior: public ──
    public async Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct)
    {
        // ...
    }

    public async Task SaveAsync(Order order, CancellationToken ct)
    {
        // ...
    }

    public void Dispose()
    {
        // ...
    }

    // ── Behavior: private ──
    private Order MapToDomain(OrderRow row)
    {
        // ...
    }
}
```

---

## Namespace and Using Rules

- File-scoped namespace declarations: `namespace MyProject.Services;`
- `using` directives outside the namespace declaration.
- `using` sort order: System → Microsoft → third-party → project.
- Prefer `using` declaration (without braces) when scope naturally matches:

```csharp
using SqlConnection connection = new(_connectionString);
string result = await connection.QuerySingleAsync<string>(sql).ConfigureAwait(false);
```

---

## Null Safety

### Public Methods and Constructors — Always Throw on Null

```csharp
public MyService(ILogger<MyService> logger, IOrderRepository orderRepo)
{
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    _orderRepo = orderRepo ?? throw new ArgumentNullException(nameof(orderRepo));
}

public void Process(string input)
{
    ArgumentNullException.ThrowIfNull(input);
    // ...
}
```

Early `return` on null is acceptable only in private methods when it is intentional logic.

---

## Error Handling

- Never catch `Exception` without rethrowing or logging.
- Never use exceptions for control flow.
- Always catch specific exception types, never bare `catch {}`.
- Exception messages must contain context: what was attempted, what value was received, why it is invalid.
- Do not create custom exception types unless the caller needs to catch them specifically — use standard BCL exceptions otherwise.
- Every `catch` block that logs must include the exception object.

```csharp
// ✅ CORRECT — specific catch, context in message
try
{
    await _connection.ExecuteAsync(sql, parameters).ConfigureAwait(false);
}
catch (SqlException ex) when (ex.Number == 2627)
{
    throw new ConflictException($"Entity with key '{id}' already exists");
}

// ❌ FORBIDDEN
try { await DoSomething(); }
catch { }
```

---

## Async / Await

- All I/O and long-running operations must use `async/await`. NEVER `.Result` or `.Wait()`.
- Async methods always return `Task` / `Task<T>`, never `async void` (exception: UI event handlers only).
- Always pass `CancellationToken` to public async methods. Name it `ct` for brevity.
- Name async methods with the `Async` suffix.

### ConfigureAwait Rules

Use `ConfigureAwait(false)` in all service, repository, helper, and library code that does not need to resume on the UI thread. This keeps infrastructure code independent from the Avalonia UI context.

Do NOT use `ConfigureAwait(false)` in:
- ViewModel methods (must resume on UI thread to update observable properties)
- Code-behind event handlers
- Any method that touches Avalonia controls or a dispatcher directly

```csharp
// ✅ Service / Repository — ConfigureAwait(false)
public async Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct)
{
    using SqlConnection connection = _connectionFactory.CreateConnection();
    OrderRow? row = await connection.QuerySingleOrDefaultAsync<OrderRow>(
        SelectByIdSql, new { Id = id.Value }).ConfigureAwait(false);
    return row is null ? null : MapToEntity(row);
}

// ✅ ViewModel — NO ConfigureAwait(false)
[RelayCommand]
private async Task LoadAsync(CancellationToken ct)
{
    IsLoading = true;
    IReadOnlyList<OrderDto> orders = await _orderService.GetOrdersAsync(ct);
    _items.Clear();
    foreach (OrderDto order in orders)
    {
        _items.Add(order);
    }
    IsLoading = false;
}
```

---

## Threading and Dispatcher Rules

- Never use `Dispatcher.UIThread.Post` / `Dispatcher.UIThread.InvokeAsync` directly from a ViewModel. Keep ViewModels testable; use `async/await`, observable state, or an injected UI-thread abstraction only when background callbacks are unavoidable.
- `Dispatcher.UIThread.Post` / `InvokeAsync` is acceptable only in views, code-behind, custom controls, and UI infrastructure services.
- For reusable controls/libraries in Avalonia 12, prefer the instance dispatcher (`AvaloniaObject.Dispatcher`) or `Dispatcher.CurrentDispatcher` over hard-coded `Dispatcher.UIThread`.
- Never perform blocking work on the UI thread — offload CPU-bound work to `Task.Run` and `await` it.
- Never capture `SynchronizationContext` manually. Avoid explicit `SynchronizationContext.Post` / `Send`.
- Do NOT use `BindingOperations.EnableCollectionSynchronization`; it is WPF-only. Bound `ObservableCollection<T>` mutations must happen on the Avalonia UI thread.

```csharp
// ✅ Load on a background thread, then replace/update bound state on the UI thread.
IReadOnlyList<OrderItem> loadedItems = await Task.Run(LoadItems, ct);
Items = new ObservableCollection<OrderItem>(loadedItems);

// ✅ UI infrastructure only: if the whole method runs on a worker thread,
// marshal only the UI-state mutation.
await Dispatcher.UIThread.InvokeAsync(() =>
{
    foreach (OrderItem item in loadedItems)
    {
        Items.Add(item);
    }
});

// ✅ ViewModel: use an injected abstraction when a background callback cannot
// naturally return to the UI thread through async/await.
await _uiThread.InvokeAsync(() =>
{
    foreach (OrderItem item in loadedItems)
    {
        Items.Add(item);
    }
}, ct);
```

---

## Collections and LINQ

- Use collection expressions to initialize: `string[] vowels = ["a", "e", "i", "o", "u"];`
- Prefer LINQ over manual loops for data transformations.
- Do not call LINQ queries multiple times — materialize with `ToList()` / `ToArray()` when needed.
- Public API methods return `IReadOnlyList<T>` / `IReadOnlyCollection<T>`, not `List<T>`.
- Use meaningful names for LINQ query variables.
- Place `where` clauses before other clauses to reduce the set early.
- Do not materialize `IEnumerable<T>` unless iterated more than once.

---

## Strings

- Use string interpolation for short concatenation: `$"{user.LastName}, {user.FirstName}"`.
- Prefer raw string literals for multi-line content.
- Never concatenate strings inside a loop — use `StringBuilder`.
- Always specify `StringComparison` explicitly: `string.Equals(a, b, StringComparison.OrdinalIgnoreCase)`.

---

## Resource Management

- Dispose resources according to ownership. If the current method creates an `IDisposable` and does not transfer ownership, wrap it in `using` / `await using`.
- Do not dispose injected dependencies, externally owned objects, returned objects, fields owned by the containing type, or UI resources whose lifetime is managed by the framework/container. The owner is responsible for disposal.
- A ViewModel that subscribes to events must implement `IDisposable` and unsubscribe in `Dispose`.

---

## Delegates and Operators

- Use `Func<>` and `Action<>` instead of custom delegate types unless a named delegate improves readability.
- Always use short-circuit `&&` and `||`. Never bitwise `&` and `|` for logical conditions.
- Call static members by declaring class: `ClassName.StaticMember`.

---

## Contract Design Rules

- Public methods accepting collections must not mutate them — treat as read-only.
- CQS: project-owned domain, service, repository, ViewModel, and application service methods with side effects return `void` / `Task`; methods that return a value have no side effects.
- Exception: command handlers may return an explicit outcome such as `Task<Result<T>>`, `Task<Result>`, a created object ID, an accepted response, or `Unit`/`Task` while changing state, because the value is the command outcome contract, not a query result. This exception applies only to command handlers and does not allow repositories or services to return success flags for side-effect methods.
- Boolean mode flags on public project-owned application, domain, service, and repository APIs are FORBIDDEN — replace with an `enum` or two clearly named methods.
- Boolean values are allowed when the boolean is the natural domain/API value, not a mode selector: `SetEnabled(bool enabled)`, `SetVisible(bool visible)`, `TryParse(..., bool ignoreCase)`.
- Framework-mandated signatures are exceptions: overrides, externally defined interface implementations, Avalonia attached property accessors, and `Dispose(bool disposing)`. These exceptions do not allow boolean mode flags in project-owned application, domain, service, repository, or ViewModel APIs.
