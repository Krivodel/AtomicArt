# CQRS + MediatR

## Core Principle

Commands express an intent to create, start, update, delete, or otherwise perform a business operation. Queries read existing state. They NEVER mix.

## Commands

Commands represent intent to perform a business operation. Named as imperative verbs.

```csharp
public record CreateOrderCommand(
    Guid CustomerId,
    Guid ProductId,
    int Quantity) : IRequest<Result<OrderDto>>;
```

`OrderDto` and other transport models live in `ProjectName.Contracts`. Application handlers map domain entities to contract DTOs through Application mapping classes.

### Command Rules

1. Named as imperative: `CreateOrderCommand`, `CancelOrderCommand`
2. Immutable: use `record`
3. External/API-facing commands use raw contract types. Value objects are created inside the handler. Internal in-process commands may accept value objects when the caller is already inside the trusted Application/Domain boundary.
4. One handler per command
5. Commands return the outcome the use case needs: `Result<T>`, `Result`, created object ID, accepted response, or `Unit`/`Task` for no payload.
6. Commands MUST represent a business operation with imperative intent: create, start, generate, update, delete, submit, approve, cancel, import, export, or similar. The production implementation will usually change state or call an external side-effecting service. A temporary fake/placeholder implementation may return generated output without persistence and still remain a command when the public use case is creation/generation.
7. Returning an explicit command outcome from a command handler is the allowed CQS exception for state-changing use cases. Repositories and services still return `Task` for side-effect operations unless their contract is explicitly a query or domain calculation.

### Command Handler Pattern

```csharp
public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, Result<OrderDto>>
{
    private readonly IOrderRepository _orderRepo;
    private readonly IProductRepository _productRepo;
    private readonly IDateTimeProvider _dateTime;
    private readonly IPublisher _publisher;

    public CreateOrderHandler(
        IOrderRepository orderRepo,
        IProductRepository productRepo,
        IDateTimeProvider dateTime,
        IPublisher publisher)
    {
        _orderRepo = orderRepo ?? throw new ArgumentNullException(nameof(orderRepo));
        _productRepo = productRepo ?? throw new ArgumentNullException(nameof(productRepo));
        _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    public async Task<Result<OrderDto>> Handle(
        CreateOrderCommand request, CancellationToken ct)
    {
        Product? product = await _productRepo
            .GetByIdAsync(new ProductId(request.ProductId), ct)
            .ConfigureAwait(false);

        if (product is null)
        {
            return Result<OrderDto>.NotFound("Product not found");
        }

        Order order = Order.Create(
            customerId: new CustomerId(request.CustomerId),
            product: product,
            quantity: Quantity.Create(request.Quantity),
            createdAt: _dateTime.UtcNow);

        await _orderRepo.SaveAsync(order, ct).ConfigureAwait(false);

        foreach (IDomainEvent domainEvent in order.DomainEvents)
        {
            INotification notification = DomainEventNotificationFactory.Create(domainEvent);
            await _publisher.Publish(notification, ct).ConfigureAwait(false);
        }
        order.ClearDomainEvents();

        OrderDto dto = OrderMappings.ToDto(order);
        return Result<OrderDto>.Success(dto);
    }
}
```

## Queries

Read data without changing state. Named as questions.

```csharp
public record GetOrderByIdQuery(Guid OrderId) : IRequest<Result<OrderDto>>;

public record GetOrdersListQuery(
    int Page,
    int PageSize,
    string? StatusFilter) : IRequest<Result<PagedList<OrderListItemDto>>>;
```

### Query Rules

1. Named as questions: `GetOrderByIdQuery`, `GetOrdersListQuery`
2. MUST NOT change state
3. Can bypass domain entities — return contract DTOs directly from read repository
4. Can have dedicated read models joining multiple tables

```csharp
public class GetOrdersListHandler
    : IRequestHandler<GetOrdersListQuery, Result<PagedList<OrderListItemDto>>>
{
    private readonly IOrderReadRepository _readRepo;

    public GetOrdersListHandler(IOrderReadRepository readRepo)
    {
        _readRepo = readRepo ?? throw new ArgumentNullException(nameof(readRepo));
    }

    public async Task<Result<PagedList<OrderListItemDto>>> Handle(
        GetOrdersListQuery request, CancellationToken ct)
    {
        PagedList<OrderListItemDto> result = await _readRepo
            .GetPagedListAsync(request.Page, request.PageSize, request.StatusFilter, ct)
            .ConfigureAwait(false);

        return Result<PagedList<OrderListItemDto>>.Success(result);
    }
}
```

## Validators

Commands that receive external/user/API input or contain non-trivial input fields must have a FluentValidation validator. Validators check format/presence, NOT business rules.

Internal commands, marker commands, and commands with no external input may omit a validator when there is nothing meaningful to validate. If a command omits a validator, the reason must be obvious from the command shape or documented in the feature/review notes.

```csharp
public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("Customer ID is required");

        RuleFor(x => x.ProductId)
            .NotEmpty()
            .WithMessage("Product ID is required");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be positive")
            .LessThanOrEqualTo(1000)
            .WithMessage("Quantity cannot exceed 1000");
    }
}
```

### Validator Rules

1. One validator per command that has external/user/API input or meaningful format/presence checks
2. Live in Application layer, next to the command
3. Check format/presence — "Quantity > 0" is format. "Customer has credit" is business (entity).
4. Run automatically via `ValidationBehavior<,>` pipeline behavior

## Pipeline Behaviors

Standard order: `Request → ValidationBehavior → LoggingBehavior → Handler → Response`

```csharp
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!_validators.Any())
        {
            return await next().ConfigureAwait(false);
        }

        ValidationContext<TRequest> context = new(request);

        ValidationResult[] validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, ct))).ConfigureAwait(false);

        List<ValidationFailure> failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next().ConfigureAwait(false);
    }
}
```

### Registration

```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CreateOrderCommand).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
});

services.AddValidatorsFromAssembly(typeof(CreateOrderCommandValidator).Assembly);
```

## Notifications (Domain Events)

Domain events are domain-owned types. MediatR notifications are Application-layer adapters around those domain events.

```csharp
public sealed record OrderCreatedNotification(OrderCreatedEvent DomainEvent) : INotification;

public class OrderCreatedEventHandler : INotificationHandler<OrderCreatedNotification>
{
    private readonly IEmailService _email;

    public OrderCreatedEventHandler(IEmailService email)
    {
        _email = email ?? throw new ArgumentNullException(nameof(email));
    }

    public async Task Handle(OrderCreatedNotification notification, CancellationToken ct)
    {
        await _email.SendOrderConfirmationAsync(notification.DomainEvent.OrderId, ct)
            .ConfigureAwait(false);
    }
}
```

### Notification Rules

1. Choose delivery semantics explicitly. Post-commit fire-and-forget handlers must not roll back the command. Pre-commit handlers, synchronous invariant checks, transactional outbox writes, and required side effects may participate in the command transaction when the use case requires it.
2. Multiple handlers per event, each handles one side effect
3. Independent — no dependency on execution order

## File Organization

```
Application/
├── Features/
│   ├── Orders/
│   │   ├── Commands/
│   │   │   ├── CreateOrder/
│   │   │   │   ├── CreateOrderCommand.cs
│   │   │   │   ├── CreateOrderHandler.cs
│   │   │   │   └── CreateOrderValidator.cs
│   │   │   └── CancelOrder/
│   │   ├── Queries/
│   │   │   └── GetOrdersList/
│   │   ├── Mappings/
│   │   └── EventHandlers/
│   └── Products/
├── Common/
│   ├── Behaviors/
│   ├── Interfaces/
│   └── Models/
└── DependencyInjection.cs

Contracts/
├── Orders/
│   ├── OrderDto.cs
│   ├── OrderListItemDto.cs
│   ├── CreateOrderRequest.cs
│   └── CreateOrderResponse.cs
└── Common/
    └── PagedList.cs
```

## Rules Summary

1. **Commands express imperative business operations, Queries read** — never mix
2. **One handler per Command/Query**
3. **External commands use raw types** — VOs created inside handler; internal commands may accept VOs
4. **Commands with external or non-trivial input have validators** — FluentValidation, separate class
5. **Validators check format, entities check business rules**
6. **Pipeline behaviors run automatically**
7. **Handlers coordinate, not compute** — logic on entities
8. **Queries can bypass entities** — contract DTOs from read repository
9. **DTOs live in Contracts** — Application maps to them but does not define externally consumed transport models
10. **Domain events adapted to notifications** — multiple independent handlers with explicit delivery semantics
11. **Feature folders** — grouped by feature, not by type
