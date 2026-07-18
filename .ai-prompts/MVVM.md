# MVVM

## Core Principle

Views display. ViewModels manage state. Services do IO. Zero business logic in code-behind.

## View Rules

### What Views Do
- Display data via `{Binding}`
- Trigger commands via `{Binding CommandName}`
- Apply Avalonia styles, control themes, templates, and resources
- Define layout in `.axaml`

### What Views Do NOT Do
- Call methods on services
- Contain business if/else logic
- Manipulate domain/application data
- Reference other Views directly

### Acceptable Code-Behind

```csharp
public partial class OrderListView : UserControl
{
    public OrderListView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is OrderListViewModel vm)
        {
            await vm.LoadCommand.ExecuteAsync(null);
        }
    }
}
```

Everything else in code-behind — REJECT unless it is purely view-specific behavior (focus, animation, drag/resize, or platform integration).

## ViewModel Rules

### Base Class and Toolkit

Use CommunityToolkit.Mvvm: `[ObservableProperty]`, `[RelayCommand]`, `[NotifyCanExecuteChangedFor]`, `[NotifyDataErrorInfo]`, `ObservableValidator`.

```csharp
public partial class OrderListViewModel : ObservableObject
{
    private readonly IOrderApiClient _apiClient;
    private readonly INavigationService _navigation;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly ObservableCollection<OrderDto> _items = [];

    public ReadOnlyObservableCollection<OrderDto> Items { get; }

    public OrderListViewModel(
        IOrderApiClient apiClient,
        INavigationService navigation,
        IViewModelErrorHandler errorHandler)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        Items = new ReadOnlyObservableCollection<OrderDto>(_items);
    }
}
```

Do not use `BindingOperations.EnableCollectionSynchronization`; it is WPF-only. In Avalonia, mutate bound collections on the UI thread, or replace a bindable snapshot property after background work completes.

### ViewModels Must NOT Reference Avalonia Types

No `Window`, `UserControl`, `TopLevel`, `AvaloniaObject`, `StyledProperty`, `IsVisible`, `Dispatcher`, `IStorageProvider`, or platform window types in ViewModels. Communicate with Views only through bindings and service interfaces (`IDialogService`, `INavigationService`, `IFilePickerService`).

ViewModels expose UI state as domain-neutral CLR values: `bool`, `string?`, DTOs, enums, and immutable/read-only collections. Do not expose Avalonia-specific visual concepts.

### Standard Properties

Every ViewModel that performs async operations MUST expose:

| Property | Type | Purpose |
|----------|------|---------|
| `IsLoading` | `bool` | Shows/hides loading, disables commands |
| `ErrorMessage` | `string?` | Displays error. `null` = no error |
| `HasErrorMessage` | `bool` | Bind directly to `IsVisible`; avoid `Visibility` converters |

```csharp
[ObservableProperty]
[NotifyCanExecuteChangedFor(nameof(LoadCommand))]
private bool _isLoading;

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(HasErrorMessage))]
private string? _errorMessage;

public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);
```

### ViewModel Error Handler

ViewModel commands translate exceptions to user-safe messages through a dedicated UI-boundary service. Never assign `ex.Message` to `ErrorMessage`.

```csharp
public interface IViewModelErrorHandler
{
    void Log(Exception exception, string operationName);
    string GetUserMessage(Exception exception);
}

public sealed class ViewModelErrorHandler : IViewModelErrorHandler
{
    private readonly ILogger<ViewModelErrorHandler> _logger;

    public ViewModelErrorHandler(ILogger<ViewModelErrorHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Log(Exception exception, string operationName)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        if (exception is HttpRequestException)
        {
            _logger.LogWarning(
                exception,
                "ViewModel operation failed due to HTTP error: {OperationName}",
                operationName);

            return;
        }

        _logger.LogError(
            exception,
            "ViewModel operation failed unexpectedly: {OperationName}",
            operationName);
    }

    public string GetUserMessage(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            HttpRequestException => "Failed to connect to server. Please try again.",
            TaskCanceledException => "The operation timed out. Please try again.",
            _ => "An unexpected error occurred. Please try again."
        };
    }
}
```

### Collections — ReadOnlyObservableCollection Wrapper

NEVER expose raw `ObservableCollection<T>` directly. Always wrap it in `ReadOnlyObservableCollection<T>` or expose an immutable/read-only snapshot.

```csharp
// ✅ CORRECT
private readonly ObservableCollection<OrderDto> _items = [];
public ReadOnlyObservableCollection<OrderDto> Items { get; }

public OrderListViewModel(IOrderApiClient apiClient)
{
    _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    Items = new ReadOnlyObservableCollection<OrderDto>(_items);
}

// ❌ FORBIDDEN — caller can mutate the collection
public ObservableCollection<OrderDto> Items { get; } = [];
```

Service and domain methods that return collections must return `IReadOnlyList<T>` — never `ObservableCollection<T>`. Convert at the ViewModel boundary only.

## Commands

Every user action maps to a command. Never wire event handlers in a ViewModel.

### Command Pattern

```csharp
public partial class OrderListViewModel : ObservableObject
{
    private readonly IOrderApiClient _apiClient;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly ObservableCollection<OrderDto> _items = [];

    public ReadOnlyObservableCollection<OrderDto> Items { get; }

    public OrderListViewModel(
        IOrderApiClient apiClient,
        IViewModelErrorHandler errorHandler)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        Items = new ReadOnlyObservableCollection<OrderDto>(_items);
    }

    [RelayCommand(CanExecute = nameof(CanLoad))]
    private async Task LoadAsync(CancellationToken ct)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            IReadOnlyList<OrderDto> orders = await _apiClient.GetOrdersAsync(ct);
            _items.Clear();

            foreach (OrderDto order in orders)
            {
                _items.Add(order);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, nameof(LoadAsync));
            ErrorMessage = _errorHandler.GetUserMessage(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanLoad()
    {
        return !IsLoading;
    }
}
```

### Command Rules

1. Every action is a command — no event handlers in code-behind except view lifecycle or view-only behavior
2. `CanExecute` must return `false` while an async command is executing — prevent double invocation
3. Async commands for IO — `[RelayCommand]` with `async Task`
4. Async commands must have a controlled error-handling strategy. Use local `try/catch`, a command wrapper, or centralized command error handling. Log through `IViewModelErrorHandler`, set a user-safe `ErrorMessage` when the ViewModel owns user feedback, and do not let unexpected exceptions reach the dispatcher without policy.
5. Loading state: `IsLoading = true` before, `false` in `finally`
6. `CancellationToken` support
7. Do not use `ConfigureAwait(false)` in ViewModel command methods that update observable state
8. Never assign `ex.Message` to `ErrorMessage`

## Validation

Implement via `INotifyDataErrorInfo`. Use `ObservableValidator` from CommunityToolkit.Mvvm.

```csharp
public partial class OrderFormViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Name is required")]
    [MinLength(2, ErrorMessage = "Name must be at least 2 characters")]
    private string _customerName = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, 1000, ErrorMessage = "Quantity must be between 1 and 1000")]
    private int _quantity;

    [RelayCommand]
    private void Submit()
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            return;
        }
        // proceed
    }
}
```

### Validation Rules

- ViewModel must inherit `ObservableValidator` or implement `INotifyDataErrorInfo` manually
- Use `[NotifyDataErrorInfo]` on generated properties that should validate on change
- Use `[Required]`, `[Range]`, `[CustomValidation]` attributes or `SetErrors` explicitly
- Avalonia displays `INotifyDataErrorInfo` errors through `DataValidationErrors`; bind `DataValidationErrors.HasErrors` / `DataValidationErrors.Errors` only in AXAML when custom visuals are required
- Never show modal dialogs from a ViewModel for validation errors

## Navigation and Dialogs

ViewModels navigate via injected interfaces, never by creating Views.

```csharp
public interface INavigationService
{
    void NavigateTo<TViewModel>() where TViewModel : ObservableObject;
    void NavigateTo<TViewModel, TParameter>(TParameter parameter)
        where TViewModel : ObservableObject;
    void GoBack();
}

public interface IDialogService
{
    Task<bool> ShowConfirmationAsync(string title, string message, CancellationToken ct = default);
    Task ShowErrorAsync(string message, CancellationToken ct = default);
}
```

### Rules

1. ViewModels never reference View types
2. `INavigationService` for screen changes
3. `IDialogService` for dialogs — no `MessageBox` or `Window` construction from ViewModels
4. Parameters are typed, not `object`
5. File/folder pickers are wrapped behind `IFilePickerService`; the implementation may use Avalonia `IStorageProvider`

## Services

### API Client Pattern

```csharp
public interface IOrderApiClient
{
    Task<IReadOnlyList<OrderDto>> GetOrdersAsync(CancellationToken ct = default);
    Task<OrderDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<OrderDto> CreateAsync(CreateOrderRequest request, CancellationToken ct = default);
}

public class OrderApiClient : IOrderApiClient
{
    private readonly HttpClient _httpClient;

    public OrderApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync(CancellationToken ct)
    {
        HttpResponseMessage response = await _httpClient
            .GetAsync("api/v1/orders", ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        List<OrderDto>? result = await response.Content
            .ReadFromJsonAsync<List<OrderDto>>(ct)
            .ConfigureAwait(false);

        return result ?? new List<OrderDto>();
    }
}
```

### Service Rules

1. Inject `HttpClient` via `IHttpClientFactory`
2. Services are stateless unless their responsibility is explicitly stateful, such as caching, navigation/session state, connection state, queues, coordinators, or debounced work. Stateful services must have a clear lifetime and ownership model.
3. Desktop services may contain limited client-side business logic only when an approved design explicitly documents it as an architectural assumption for an operational reason, such as reducing server load, avoiding excessive API chatter, or keeping interactive previews responsive. This logic must use only Contracts DTOs and local client state, must remain a preview/hint/projection, and must not be the source of truth for persistence, authorization, billing, server validation, paid execution, or irreversible business decisions.
4. Use `ConfigureAwait(false)` in services
5. Throw on failure — ViewModel catches and translates

## Threading

- NEVER use Avalonia `Dispatcher` directly from a ViewModel
- Use `async/await` to return to the UI thread naturally after awaited service calls
- Mutate bound `ObservableCollection<T>` only on the UI thread
- For CPU-bound work, compute on a background thread and update observable state after the `await`
- If a background callback must update bound state, inject a UI-thread abstraction instead of referencing Avalonia directly
- Never capture `SynchronizationContext` manually

```csharp
public interface IUiThreadDispatcher
{
    Task InvokeAsync(Action action, CancellationToken ct = default);
}

// Implementation lives in the Avalonia UI layer, not in application/domain code.
public sealed class AvaloniaUiThreadDispatcher : IUiThreadDispatcher
{
    public async Task InvokeAsync(Action action, CancellationToken ct = default)
    {
        await Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Default, ct);
    }
}
```

## Memory Leak Prevention

- Never subscribe to events on long-lived objects without unsubscribing in `Dispose`
- ViewModel that subscribes to events must implement `IDisposable` and unsubscribe in `Dispose`
- Prefer event subscriptions that return `IDisposable` handles when designing app services
- Use Avalonia weak-event utilities only inside the Avalonia UI layer; do not leak `Avalonia.Utilities` into application/domain layers

```csharp
public sealed class OrderListViewModel : ObservableObject, IDisposable
{
    private readonly IOrderNotificationService _notifications;

    public OrderListViewModel(IOrderNotificationService notifications)
    {
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _notifications.OrderUpdated += OnOrderUpdated;
    }

    public void Dispose()
    {
        _notifications.OrderUpdated -= OnOrderUpdated;
    }

    private void OnOrderUpdated(object? sender, OrderUpdatedEventArgs e)
    {
        // Update state.
    }
}
```

- Avoid closures in event handlers that capture `this` or large objects unnecessarily
- Never bind to a property of an object that does not implement `INotifyPropertyChanged` when the property can change

## DI Registration

```csharp
string apiBaseUrl = configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("Configuration value 'ApiBaseUrl' is missing");

services.AddHttpClient<IOrderApiClient, OrderApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

services.AddTransient<OrderListViewModel>();
services.AddTransient<OrderDetailViewModel>();
services.AddSingleton<INavigationService, NavigationService>();
services.AddSingleton<IDialogService, DialogService>();
services.AddSingleton<IViewModelErrorHandler, ViewModelErrorHandler>();
services.AddSingleton<IUiThreadDispatcher, AvaloniaUiThreadDispatcher>();
```

| Service | Lifetime | Why |
|---------|----------|-----|
| ViewModels | Transient | New instance per navigation |
| INavigationService | Singleton | One navigation stack |
| IDialogService | Singleton | One dialog manager |
| IViewModelErrorHandler | Singleton | Centralized user-safe error translation and logging |
| IUiThreadDispatcher | Singleton | UI dispatcher facade |
| API clients | Transient (via HttpClientFactory) | Managed by factory |

## Split Large ViewModels

Each ViewModel covers one area of responsibility.

ViewModel length is a recommendation, not a blocker by itself. Treat 200–300 lines as a prompt to check whether the ViewModel has multiple responsibilities, excessive control flow, duplicated logic, or hard-to-test behavior.

Split a large ViewModel only when it produces a concrete benefit: clearer responsibility boundaries, lower duplication, easier testing, or simpler state management. Do not split a ViewModel only to satisfy a line-count target.

## Rules Summary

1. **Views have NO business logic** — AXAML bindings + `InitializeComponent`
2. **ViewModels have NO Avalonia type references**
3. **Every action is a Command** — `[RelayCommand]`
4. **CanExecute returns false while executing** — prevent double invocation
5. **IsLoading + ErrorMessage + HasErrorMessage** — mandatory for async ViewModels
6. **ReadOnlyObservableCollection** — never expose raw ObservableCollection
7. **INotifyDataErrorInfo for validation** — `ObservableValidator` + `[NotifyDataErrorInfo]`
8. **Controlled async command error handling** — local or centralized strategy, log through `IViewModelErrorHandler`, show safe messages when user feedback is owned by the ViewModel
9. **Services injected, never `new`**
10. **No Dispatcher in ViewModels** — async/await + UI-thread-owned collections
11. **Dispose event subscriptions** — prevent memory leaks
12. **ConfigureAwait(false) in services** — never in ViewModels that update UI-bound state
