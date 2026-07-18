# Tests Style

## Framework

- **Test framework:** xUnit
- **Mocking:** Moq (or NSubstitute — match existing project)
- **Assertions:** FluentAssertions (preferred) or xUnit built-in
- **Test data:** manual construction, NOT AutoFixture (domain objects need meaningful data)

## Test Naming

Pattern: `MethodName_Scenario_ExpectedResult`

```csharp
[Fact]
public void Create_WithValidData_ReturnsOrder() { }

[Fact]
public void Create_WithNegativeQuantity_ThrowsDomainException() { }

[Fact]
public void Cancel_WhenStatusIsPending_SetsStatusToCancelled() { }

[Fact]
public async Task LoadOrdersAsync_ServiceThrows_SetsErrorMessage() { }
```

### Naming Rules

1. Class name: `{ClassUnderTest}Tests`
2. Method name: `{Method}_{Scenario}_{ExpectedResult}`
3. No "Test" prefix — `[Fact]` attribute is enough
4. No "Should" — direct result: `ThrowsDomainException`, not `ShouldThrow`
5. Be specific: `WithNegativeQuantity`, not `WithBadData`

## AAA Pattern

Arrange–Act–Assert with clear visual separation by blank lines only.

```csharp
[Fact]
public void Create_WithValidData_ReturnsOrder()
{
    CustomerId customerId = new(TestIds.CustomerId);
    Product product = CreateTestProduct(price: 10.00m);
    Quantity quantity = Quantity.Create(5);
    DateTime createdAt = TestClock.CreatedAt;

    Order order = Order.Create(customerId, product, quantity, createdAt);

    order.Id.Should().NotBe(default);
    order.CustomerId.Should().Be(customerId);
    order.Status.Should().Be(OrderStatus.Pending);
    order.TotalAmount.Amount.Should().Be(50.00m);
    order.CreatedAt.Should().Be(createdAt);
}
```

### Rules

1. Blank line between Arrange, Act, and Assert blocks is mandatory
2. AAA comments are forbidden
3. Arrange is explicit — create all test data, no shared mutable state
4. In narrow unit tests, Act is one call — one action per test. Scenario, integration, UI, and regression tests may perform several actions when those actions are the behavior under test.
5. Assert is focused — assert what THIS test verifies

## One Concept Per Test

Each test verifies ONE concept. Multiple assertions on the same result are fine. Multiple operations are forbidden in narrow unit tests when they test unrelated behavior. Scenario, integration, UI, and regression tests may execute a workflow when the workflow is the single concept under test.

Correct example:

```csharp
[Fact]
public void Create_WithValidData_ReturnsOrderWithCorrectProperties()
{
    CustomerId customerId = new(TestIds.CustomerId);
    Product product = CreateTestProduct(price: 10.00m);
    Quantity quantity = Quantity.Create(3);
    DateTime createdAt = TestClock.CreatedAt;

    Order order = Order.Create(customerId, product, quantity, createdAt);

    order.CustomerId.Should().Be(customerId);
    order.Status.Should().Be(OrderStatus.Pending);
    order.TotalAmount.Amount.Should().Be(30.00m);
}
```

Wrong example:

```csharp
[Fact]
public void CreateAndCancel_SetsStatusToCancelled()
{
    Order order = Order.Create(...);
    order.Cancel(TestClock.CancelledAt);
    order.Status.Should().Be(OrderStatus.Cancelled);
}
```

## Test Data

Helper methods. Use `Restore` for persisted entity state that is only test setup. Use `Create` when the test verifies creation invariants, factory behavior, validation, or domain events. Test values are deterministic by default.

```csharp
internal static class TestIds
{
    public static Guid OrderId { get; } =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static Guid CustomerId { get; } =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static Guid ProductId { get; } =
        Guid.Parse("33333333-3333-3333-3333-333333333333");
}

internal static class TestClock
{
    public static DateTime CreatedAt { get; } =
        new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    public static DateTime CancelledAt { get; } =
        new DateTime(2024, 1, 16, 10, 0, 0, DateTimeKind.Utc);
}

public class OrderTests
{
    private static Product CreateTestProduct(decimal price = 10.00m)
    {
        return Product.Restore(
            id: new ProductId(TestIds.ProductId),
            name: ProductName.Create("Test Product"),
            price: Money.Create(price, Currency.USD));
    }

    private static Order CreateTestOrder(OrderStatus status = OrderStatus.Pending)
    {
        return Order.Restore(
            id: new OrderId(TestIds.OrderId),
            customerId: new CustomerId(TestIds.CustomerId),
            status: status,
            totalAmount: Money.Create(100m, Currency.USD),
            createdAt: TestClock.CreatedAt);
    }

    [Fact]
    public void Cancel_WhenPending_SetsStatusToCancelled()
    {
        Order order = CreateTestOrder(OrderStatus.Pending);

        order.Cancel(TestClock.CancelledAt);

        order.Status.Should().Be(OrderStatus.Cancelled);
    }
}
```

### Test Data Rules

1. Use `Restore` for persisted entity state that is only test setup — skip validation and creation side effects
2. Use `Create` when the test verifies creation invariants, factory behavior, validation, or domain events
3. Explicit data — no uncontrolled random/auto-generated values
4. Defaults in helpers — override only what's relevant
5. No shared mutable state — each test creates its own objects
6. Current time (`DateTime.UtcNow`, `DateTime.Now`) and uncontrolled randomness are forbidden in tests
7. Prefer fixed `Guid.Parse(...)` values or shared deterministic test constants
8. Unique values such as `Guid.NewGuid()`, `OrderId.New()`, or generated suffixes are allowed only when uniqueness is the behavior under test or when per-test uniqueness is needed for isolation. Keep the uniqueness local to the test and do not use it for expected values unless the test captures and asserts the generated value.

## Mocking

### Handler Tests

```csharp
public class CreateOrderHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepoMock = new();
    private readonly Mock<IProductRepository> _productRepoMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeMock = new();
    private readonly Mock<IPublisher> _publisherMock = new();
    private readonly CreateOrderHandler _handler;

    public CreateOrderHandlerTests()
    {
        _dateTimeMock.Setup(x => x.UtcNow)
            .Returns(TestClock.CreatedAt);

        _handler = new CreateOrderHandler(
            _orderRepoMock.Object,
            _productRepoMock.Object,
            _dateTimeMock.Object,
            _publisherMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_SavesOrder()
    {
        Product product = CreateTestProduct();
        _productRepoMock
            .Setup(x => x.GetByIdAsync(It.IsAny<ProductId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        CreateOrderCommand command = new(
            CustomerId: TestIds.CustomerId,
            ProductId: product.Id.Value,
            Quantity: 3);

        Result<OrderDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _orderRepoMock.Verify(
            x => x.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenProductNotFound_ReturnsNotFoundAndDoesNotSave()
    {
        _productRepoMock
            .Setup(x => x.GetByIdAsync(It.IsAny<ProductId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        CreateOrderCommand command = new(TestIds.CustomerId, TestIds.ProductId, 1);

        Result<OrderDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsNotFound.Should().BeTrue();
        _orderRepoMock.Verify(
            x => x.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
```

### Mocking Rules

1. Prefer mocking interfaces, not concrete classes. Concrete classes, abstract framework types, and other test doubles are allowed when an interface seam does not exist, when working with legacy/framework code, or when a fake/stub is clearer than a mock.
2. Setup only what the test needs
3. Verify meaningful interactions
4. `It.IsAny<T>()` for irrelevant params
5. Return realistic data

## Exception Tests

```csharp
[Fact]
public void Cancel_WhenStatusIsShipped_ThrowsDomainExceptionWithCorrectCode()
{
    Order order = CreateTestOrder(OrderStatus.Shipped);

    Action act = () => order.Cancel(TestClock.CancelledAt);

    act.Should().Throw<DomainException>()
        .Which.ErrorCode.Should().Be("ERR-ORD-001");
}
```

## Validator Tests

```csharp
public class CreateOrderCommandValidatorTests
{
    private readonly CreateOrderCommandValidator _validator = new();

    [Fact]
    public void Validate_WithEmptyCustomerId_HasValidationError()
    {
        CreateOrderCommand command = new(
            CustomerId: Guid.Empty,
            ProductId: TestIds.ProductId,
            Quantity: 1);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CustomerId");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_WithNonPositiveQuantity_HasValidationError(int quantity)
    {
        CreateOrderCommand command = new(TestIds.CustomerId, TestIds.ProductId, quantity);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Quantity");
    }
}
```

## ViewModel Tests

All public ViewModel logic must be covered: commands, computed properties, validation, state transitions.

```csharp
public class OrderListViewModelTests
{
    private readonly Mock<IOrderApiClient> _apiClientMock = new();
    private readonly Mock<IViewModelErrorHandler> _errorHandlerMock = new();
    private readonly OrderListViewModel _viewModel;

    public OrderListViewModelTests()
    {
        _viewModel = new OrderListViewModel(
            _apiClientMock.Object,
            _errorHandlerMock.Object);
    }

    [Fact]
    public async Task LoadCommand_WhenServiceReturnsData_PopulatesItems()
    {
        List<OrderDto> orders = [new OrderDto(TestIds.OrderId, "Pending", 100m, TestClock.CreatedAt)];
        _apiClientMock
            .Setup(x => x.GetOrdersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(orders);

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.Items.Should().HaveCount(1);
        _viewModel.IsLoading.Should().BeFalse();
        _viewModel.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task LoadCommand_WhenServiceThrows_SetsErrorMessage()
    {
        HttpRequestException exception = new("Connection failed");
        _apiClientMock
            .Setup(x => x.GetOrdersAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        _errorHandlerMock
            .Setup(x => x.GetUserMessage(exception))
            .Returns("Failed to connect to server. Please try again.");

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _errorHandlerMock.Verify(
            x => x.Log(exception, "LoadAsync"),
            Times.Once);
        _viewModel.ErrorMessage.Should().Be("Failed to connect to server. Please try again.");
        _viewModel.IsLoading.Should().BeFalse();
    }
}
```

## Test Project Structure

Mirror the source project structure:

```
tests/
├── ProjectName.Domain.Tests/
│   ├── Entities/
│   └── ValueObjects/
├── ProjectName.Contracts.Tests/
├── ProjectName.Application.Tests/
│   └── Features/Orders/
│       ├── Commands/
│       ├── Queries/
│       └── Validators/
├── ProjectName.Infrastructure.Tests/
│   └── Repositories/
└── ProjectName.Desktop.Tests/
    └── ViewModels/
```

## Rules Summary

1. **`MethodName_Scenario_ExpectedResult`** — consistent naming
2. **AAA with blank lines only** — Arrange, Act, Assert separated without comments
3. **One concept per test**
4. **No shared mutable state**
5. **Helper methods with `Restore` for persisted setup, `Create` for creation invariants**
6. **Prefer interface mocks** — use other test doubles when they are the right seam
7. **Test validators independently**
8. **FluentAssertions for exceptions**
9. **Mirror source structure**
10. **All public ViewModel logic covered** — commands, properties, validation, state
11. **Deterministic test data by default** — unique generated values only for isolation or uniqueness behavior, never uncontrolled current clock values
