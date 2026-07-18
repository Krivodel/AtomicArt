# Domain Model

## Core Principles

Domain entities encapsulate state AND behavior. They enforce their own invariants. They are always in a valid state.

## Entity Rules

### 1. No Public Setters

```csharp
// ✅ CORRECT
public class Order
{
    public OrderId Id { get; private set; }
    public OrderStatus Status { get; private set; }
    public Money TotalAmount { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public void Cancel(DateTime cancelledAt)
    {
        if (Status != OrderStatus.Pending)
        {
            throw new DomainException("ERR-ORD-001", "Only pending orders can be cancelled");
        }

        Status = OrderStatus.Cancelled;
    }
}

// ❌ FORBIDDEN — public setters
public class Order
{
    public Guid Id { get; set; }
    public string Status { get; set; }
    public decimal Total { get; set; }
}
```

### 2. Factory Methods for Creation

Entities are created through static factory methods that enforce invariants. Do not expose a public parameterless constructor. A private parameterless constructor is allowed for ORM materialization or `Restore`.

```csharp
public class Order
{
    private Order() { }

    public static Order Create(
        CustomerId customerId,
        Product product,
        Quantity quantity,
        DateTime createdAt)
    {
        if (quantity.Value <= 0)
        {
            throw new DomainException("ERR-ORD-002", "Quantity must be positive");
        }

        Order order = new()
        {
            Id = OrderId.New(),
            CustomerId = customerId,
            Status = OrderStatus.Pending,
            TotalAmount = product.Price * quantity,
            CreatedAt = createdAt
        };

        order.AddDomainEvent(new OrderCreatedEvent(order.Id));
        return order;
    }

    public static Order Restore(
        OrderId id,
        CustomerId customerId,
        OrderStatus status,
        Money totalAmount,
        DateTime createdAt)
    {
        return new Order
        {
            Id = id,
            CustomerId = customerId,
            Status = status,
            TotalAmount = totalAmount,
            CreatedAt = createdAt
        };
    }
}
```

### 3. Restore Method for Persistence

Persisted aggregate roots and entities reconstructed by repositories must have a `Restore` static method used only by repositories or persistence mappers. `Restore` does not validate — persisted data was validated at creation.

Do not require `Restore` for entities that are not persisted independently, are created only inside an aggregate, or are never reconstructed from storage.

```csharp
// Repository uses Restore to map from DB
public async Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct)
{
    using SqlConnection connection = _connectionFactory.CreateConnection();
    OrderRow? row = await connection.QuerySingleOrDefaultAsync<OrderRow>(
        SelectByIdSql, new { Id = id.Value }).ConfigureAwait(false);

    if (row is null)
    {
        return null;
    }

    return Order.Restore(
        id: new OrderId(row.Id),
        customerId: new CustomerId(row.CustomerId),
        status: Enum.Parse<OrderStatus>(row.Status),
        totalAmount: Money.Create(row.Amount, Enum.Parse<Currency>(row.Currency)),
        createdAt: row.CreatedAt);
}
```

### 4. Domain Methods Enforce Invariants

```csharp
public void Ship(DateTime shippedAt, TrackingNumber tracking)
{
    if (Status != OrderStatus.Confirmed)
    {
        throw new DomainException("ERR-ORD-003",
            $"Cannot ship order in '{Status}' state. Must be 'Confirmed'.");
    }

    Status = OrderStatus.Shipped;
    ShippedAt = shippedAt;
    TrackingNumber = tracking;
    AddDomainEvent(new OrderShippedEvent(Id, tracking));
}
```

### 5. Entities Are Always Valid

No point in time when an entity exists in an invalid state. Invariants checked at creation and at every state change. Never at reconstruction (Restore).

## Value Objects

Defined by value, not identity. Immutable. Self-validating.

```csharp
// ✅ Value Object as record
public record Money
{
    public decimal Amount { get; }
    public Currency Currency { get; }

    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Create(decimal amount, Currency currency)
    {
        if (amount < 0)
        {
            throw new DomainException("ERR-VO-001", "Amount cannot be negative");
        }

        return new Money(amount, currency);
    }

    public static Money Zero(Currency currency)
    {
        return new Money(0, currency);
    }
}

// ✅ Typed ID
public record OrderId
{
    public Guid Value { get; }

    public OrderId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("ERR-VO-002", "OrderId cannot be empty");
        }

        Value = value;
    }

    public static OrderId New()
    {
        return new OrderId(Guid.NewGuid());
    }
}

// ✅ Constrained string
public record Email
{
    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    public static Email Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("ERR-VO-003", "Email cannot be empty");
        }

        if (!value.Contains('@'))
        {
            throw new DomainException("ERR-VO-004", "Email must contain @");
        }

        return new Email(value.Trim().ToLowerInvariant());
    }
}
```

### Typed IDs

Persisted aggregate roots and entities with their own persistent identity MUST have a typed ID. Prevents passing wrong ID to wrong method.

Typed IDs are optional for nested entities, owned entities, temporary models, and non-persisted objects when they do not have an identity independent from their owner or current workflow.

```csharp
// ✅ Compiler prevents mixing up IDs
Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct);
Task<Customer?> GetByIdAsync(CustomerId id, CancellationToken ct);
```

## Domain Events

Raised by entities as domain-owned event types. Domain events MUST NOT depend on MediatR or other application-layer libraries. Adapt domain events to MediatR notifications in the Application layer if MediatR is used for dispatch.

```csharp
public interface IDomainEvent
{
}

public record OrderCreatedEvent(OrderId OrderId) : IDomainEvent;

public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents
    {
        get { return _domainEvents.AsReadOnly(); }
    }

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
```

## Domain Exceptions

Typed with error codes. Pattern: `ERR-{MODULE}-{NNN}`.

```csharp
public class DomainException : Exception
{
    public string ErrorCode { get; }

    public DomainException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
```

## Enums for State

State transitions enforced by entity methods.

```csharp
public enum OrderStatus
{
    Pending,
    Confirmed,
    Shipped,
    Delivered,
    Cancelled
}
// Pending → Confirmed (Confirm()), Pending → Cancelled (Cancel()),
// Confirmed → Shipped (Ship()), Shipped → Delivered (Deliver())
```

## Rules Summary

1. **No public setters** — state changes through domain methods only
2. **Factory methods** — `Create()` validates, `Restore()` only for persisted entities reconstructed from storage
3. **Always valid** — no invalid state is possible
4. **Value objects for domain concepts** — immutable records with self-validation
5. **Typed IDs for persisted identity** — `OrderId`, `CustomerId`, not raw `Guid`
6. **Domain methods enforce transitions** — throw `DomainException` on violation
7. **Domain events** — domain-owned event types raised by entities, adapted to MediatR in Application if needed
8. **Error codes** — `ERR-{MODULE}-{NNN}` pattern
9. **No persistence concerns** — no ORM attributes, no SQL
10. **Restore bypasses validation** — use only when reconstructing previously persisted valid data
