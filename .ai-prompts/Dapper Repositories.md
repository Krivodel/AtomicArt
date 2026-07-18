# Dapper Repositories

## Core Principle

Repositories are the default place where domain persistence SQL exists. They translate between domain entities and database rows.

SQL is also allowed in infrastructure-owned migration scripts, health checks, seed scripts, read-model builders, diagnostic queries, and operational tooling when those queries are not domain repository behavior.

## Connection Management

### IDbConnectionFactory

```csharp
public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}

public class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found");
    }

    public IDbConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }
}
```

### Connection Lifetime

Repository methods may manage their own connection via `using` when they own the database operation.

When a use case spans multiple repositories or must share a transaction, allow an external connection/transaction scope such as Unit of Work. In that case the scope owns disposal and commit/rollback; repository methods must not dispose the externally owned connection or transaction.

```csharp
public async Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct)
{
    using SqlConnection connection = (SqlConnection)_connectionFactory.CreateConnection();
    OrderRow? row = await connection.QuerySingleOrDefaultAsync<OrderRow>(
        SelectByIdSql, new { Id = id.Value }).ConfigureAwait(false);
    return row is null ? null : MapToEntity(row);
}
```

### Transactions

```csharp
public async Task SaveOrderWithItemsAsync(Order order, CancellationToken ct)
{
    using SqlConnection connection = (SqlConnection)_connectionFactory.CreateConnection();
    connection.Open();
    using SqlTransaction transaction = connection.BeginTransaction();

    try
    {
        await connection.ExecuteAsync(
            InsertOrderSql,
            MapToParams(order),
            transaction: transaction).ConfigureAwait(false);

        foreach (OrderItem item in order.Items)
        {
            await connection.ExecuteAsync(
                InsertOrderItemSql,
                MapItemToParams(item, order.Id),
                transaction: transaction).ConfigureAwait(false);
        }

        transaction.Commit();
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
```

## SQL Rules

### 1. Always Parameterized

```csharp
// ✅ CORRECT
await connection.QuerySingleOrDefaultAsync<OrderRow>(
    "SELECT Id, Status FROM Orders WHERE Id = @Id AND Status = @Status",
    new { Id = id.Value, Status = status.ToString() }).ConfigureAwait(false);

// ❌ FORBIDDEN — SQL injection
await connection.QuerySingleOrDefaultAsync<OrderRow>(
    $"SELECT * FROM Orders WHERE Id = '{id.Value}'");
```

### 2. SQL as Private Constants

```csharp
public class OrderRepository : IOrderRepository
{
    private const string SelectByIdSql = @"
        SELECT Id, CustomerId, Status, Amount, Currency, CreatedAt, RowVersion
        FROM Orders
        WHERE Id = @Id";

    private const string InsertSql = @"
        INSERT INTO Orders (Id, CustomerId, Status, Amount, Currency, CreatedAt)
        VALUES (@Id, @CustomerId, @Status, @Amount, @Currency, @CreatedAt)";

    private const string UpdateSql = @"
        UPDATE Orders
        SET Status = @Status, Amount = @Amount, Currency = @Currency
        WHERE Id = @Id AND RowVersion = @RowVersion";
}
```

### 3. Explicit Column Lists

Always list columns explicitly. Never `SELECT *` in production.

### 4. Optimistic Concurrency

Use optimistic concurrency for mutable data that can be edited by more than one writer and must prevent lost updates. It is optional for reference data, append-only tables, read models, single-writer tables, and other data where lost-update protection is not required.

```csharp
public async Task UpdateAsync(Order order, CancellationToken ct)
{
    using SqlConnection connection = (SqlConnection)_connectionFactory.CreateConnection();

    int rowsAffected = await connection.ExecuteAsync(UpdateSql, new
    {
        Id = order.Id.Value,
        Status = order.Status.ToString(),
        Amount = order.TotalAmount.Amount,
        RowVersion = order.RowVersion
    }).ConfigureAwait(false);

    if (rowsAffected == 0)
    {
        throw new ConcurrencyException($"Order '{order.Id.Value}' was modified by another process");
    }
}
```

## Mapping Rules

### Private Row Classes

Internal to the repository, NOT shared.

```csharp
private class OrderRow
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string? Status { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[]? RowVersion { get; set; }
}
```

### Entity → Row (INSERT/UPDATE)

```csharp
private static object MapToParams(Order order)
{
    return new
    {
        Id = order.Id.Value,
        CustomerId = order.CustomerId.Value,
        Status = order.Status.ToString(),
        Amount = order.TotalAmount.Amount,
        Currency = order.TotalAmount.Currency.ToString(),
        CreatedAt = order.CreatedAt
    };
}
```

### Row → Entity (SELECT)

Use `Restore` when the entity has a restore path for previously persisted valid state. Do not call `Create` from database rows only to reconstruct saved state, because `Create` may rerun creation validation and raise creation events.

If a model does not define `Restore`, map rows through the persistence-specific reconstruction method provided by that model. Do not invent invalid construction paths in the repository.

```csharp
private static Order MapToEntity(OrderRow row)
{
    if (row.Status is null)
    {
        throw new DataException($"Orders.Status is null for order '{row.Id}'");
    }

    if (row.Currency is null)
    {
        throw new DataException($"Orders.Currency is null for order '{row.Id}'");
    }

    if (row.RowVersion is null)
    {
        throw new DataException($"Orders.RowVersion is null for order '{row.Id}'");
    }

    return Order.Restore(
        id: new OrderId(row.Id),
        customerId: new CustomerId(row.CustomerId),
        status: Enum.Parse<OrderStatus>(row.Status),
        totalAmount: Money.Create(row.Amount, Enum.Parse<Currency>(row.Currency)),
        createdAt: row.CreatedAt);
}
```

| Direction | Method | Validates? | Events? |
|-----------|--------|-----------|---------|
| Create new entity | `Entity.Create(...)` | ✅ | ✅ |
| Load from DB | `Entity.Restore(...)` | ❌ | ❌ |
| Save to DB | Anonymous object with `.Value` | N/A | N/A |

## Read Repositories

For CQRS queries — return contract DTOs directly, skip entity reconstruction.

```csharp
public class OrderReadRepository : IOrderReadRepository
{
    private const string PagedListSql = @"
        SELECT o.Id, o.Status, o.Amount, o.Currency, o.CreatedAt,
               c.Name AS CustomerName
        FROM Orders o
        JOIN Customers c ON c.Id = o.CustomerId
        WHERE (@StatusFilter IS NULL OR o.Status = @StatusFilter)
        ORDER BY o.CreatedAt DESC
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

    public async Task<PagedList<OrderListItemDto>> GetPagedListAsync(
        int page, int pageSize, string? statusFilter, CancellationToken ct)
    {
        using SqlConnection connection = (SqlConnection)_connectionFactory.CreateConnection();

        int totalCount = await connection.ExecuteScalarAsync<int>(
            CountSql, new { StatusFilter = statusFilter }).ConfigureAwait(false);

        IEnumerable<OrderListItemDto> items = await connection.QueryAsync<OrderListItemDto>(
            PagedListSql,
            new
            {
                StatusFilter = statusFilter,
                Offset = (page - 1) * pageSize,
                PageSize = pageSize
            }).ConfigureAwait(false);

        return new PagedList<OrderListItemDto>(items.ToList(), totalCount, page, pageSize);
    }
}
```

## DI Registration

```csharp
public static IServiceCollection AddInfrastructureServices(
    this IServiceCollection services, IConfiguration configuration)
{
    services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
    services.AddScoped<IOrderRepository, OrderRepository>();
    services.AddScoped<IOrderReadRepository, OrderReadRepository>();
    return services;
}
```

| Service | Lifetime | Why |
|---------|----------|-----|
| `IDbConnectionFactory` | Singleton | Stateless, creates connections |
| `IXxxRepository` | Scoped | One per request |
| `IXxxReadRepository` | Scoped | One per request |

## Rules Summary

1. **Domain persistence SQL lives in repositories by default** — migrations, health checks, seed scripts, read-model builders, diagnostics, and operational tooling are allowed infrastructure exceptions
2. **Always parameterized** — `@param` syntax, NEVER concatenation
3. **IDbConnectionFactory** — never inject connection strings
4. **Connection ownership is explicit** — use `using` when the method owns the connection; accept an external connection/transaction scope for Unit of Work scenarios
5. **Private row classes** — internal to repository
6. **Restore for loading persisted entities** — use the model's reconstruction path; do not run creation behavior when mapping saved rows
7. **Explicit column lists** — no `SELECT *`
8. **Optimistic concurrency where needed** — RowVersion/concurrency token in `WHERE` for mutable rows that require lost-update protection
9. **Read repositories for queries** — contract DTOs directly
10. **SQL as private constants** — top of class
