# Logging and Security

## Logging Rules

### Always Use ILogger<T>

Never use `Console.Write` / `Debug.Write`. Always log through `ILogger<T>`.

### Structured Logging — No String Interpolation

```csharp
// ✅ CORRECT — structured logging
_logger.LogInformation("User {UserId} logged in from {IpAddress}", userId, ipAddress);
_logger.LogWarning("Order {OrderId} payment retry attempt {Attempt}", orderId, attempt);

// ❌ FORBIDDEN — string interpolation
_logger.LogInformation($"User {userId} logged in");
_logger.LogWarning($"Order {orderId} retry {attempt}");
```

### Log Levels

| Level | When |
|-------|------|
| `Debug` | Internal implementation details, variable dumps |
| `Information` | Business events: user logged in, order created, payment processed |
| `Warning` | Expected anomalies: retry, timeout, fallback used, rate limit approached |
| `Error` | Failures with full exception |

### Exception Logging

Always pass the exception as the FIRST argument:

```csharp
// ✅ CORRECT
_logger.LogError(ex, "Failed to process order {OrderId}", orderId);
_logger.LogWarning(ex, "Transient failure for user {UserId}, retrying", userId);

// ❌ WRONG — exception not passed
_logger.LogError("Failed to process order {OrderId}: {Message}", orderId, ex.Message);
```

### Never Log Sensitive Data

Never log passwords, tokens, API keys, PII (emails, phone numbers, SSN), or any authentication credentials.

### User-Facing Error Messages

Never show raw exception messages, stack traces, SQL errors, file paths, tokens, connection strings, or internal identifiers to users. UI and API boundaries must translate exceptions into user-safe messages and log the full exception separately through `ILogger<T>`.

Avalonia ViewModels must follow `MVVM.md`: log command exceptions through `IViewModelErrorHandler` and assign only user-safe messages to `ErrorMessage`. `ErrorMessage = ex.Message` is forbidden.

---

## Global Exception Handling

Avalonia applications must subscribe to the Avalonia UI-thread dispatcher hook and the relevant CLR-level hooks at the UI application entry point (`App.axaml.cs` or composition root):

Libraries, test projects, API-only projects, and headless processes must define exception handling at their own process/composition boundary instead of blindly copying Avalonia UI hooks.

```csharp
// In App.axaml.cs or composition root
Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
```

Optional filtering for expected cancellation/noise:

```csharp
Dispatcher.UIThread.UnhandledExceptionFilter += (sender, e) =>
{
    if (e.Exception is TaskCanceledException)
    {
        e.RequestCatch = false;
    }
};
```

### Handler Implementations

```csharp
private void OnDispatcherUnhandledException(
    object? sender, DispatcherUnhandledExceptionEventArgs e)
{
    _logger.LogError(e.Exception, "Unhandled Avalonia UI-thread exception");
    // Show user-friendly error via IDialogService
    // Set e.Handled = true ONLY if the application can safely continue
}

private void OnUnobservedTaskException(
    object? sender, UnobservedTaskExceptionEventArgs e)
{
    _logger.LogError(e.Exception, "Unobserved task exception");
    if (_processExceptionPolicy.CanContinueAfterUnobservedTaskException(e.Exception))
    {
        e.SetObserved();
    }
}

private void OnDomainUnhandledException(
    object sender, UnhandledExceptionEventArgs e)
{
    if (e.ExceptionObject is Exception ex)
    {
        _logger.LogError(ex, "Unhandled domain exception (IsTerminating: {IsTerminating})",
            e.IsTerminating);
    }
    // Last resort before crash — log only, do not attempt recovery
}
```

### Rules

1. Avalonia UI applications register the dispatcher hook and the relevant CLR-level hooks at the application entry point. Other project types use exception handling appropriate to their process boundary.
2. `Dispatcher.UIThread.UnhandledException`: log, show user-friendly error via `IDialogService`, set `e.Handled = true` only if safe to continue
3. `UnobservedTaskException`: log the full exception. Call `e.SetObserved()` only when the process exception policy allows continuing; otherwise leave the exception unobserved so the failure is visible to the configured runtime policy.
4. `AppDomain.UnhandledException`: log only — last resort before crash
5. Never silently swallow exceptions — every exception produces a log entry

---

## Security Rules

### No Hardcoded Secrets

Never hardcode secrets, connection strings, or API keys in source code or `appsettings.json` committed to the repository. Use environment variables or a secrets manager via `IConfiguration`.

### Input Validation

Never trust external input without explicit validation. Validate at the boundary (controller, API client) before it reaches the domain layer.

### No String Concatenation for Queries or Paths

Never build SQL queries, file paths, or shell commands by string concatenation. Use:
- Parameterized queries for Dapper (`@param` syntax)
- `Path.Combine` for file paths

### API Security

- `[Authorize]` attribute on all protected endpoints
- User identity from `HttpContext.User.Claims`, never from request headers directly
- No internal error details (stack traces, DB errors) in API responses — ProblemDetails middleware
- No CORS wildcard origins with credentials

### Data Protection

- No logging of passwords, tokens, PII
- No unsafe type casts without null checks
- No race conditions on shared state without proper synchronization

## Rules Summary

1. **ILogger<T> always** — never Console.Write / Debug.Write
2. **Structured logging** — never string interpolation in log messages
3. **Exception as first arg** — in LogError / LogWarning
4. **Never log secrets/PII**
5. **Three global handlers** — Avalonia UI dispatcher, UnobservedTask, AppDomain
6. **No hardcoded secrets**
7. **Parameterized queries** — never string concatenation for SQL
8. **[Authorize] on protected endpoints**
9. **ProblemDetails for API errors** — no stack traces exposed
10. **User-safe UI errors** — never show raw `ex.Message` to users
11. **Path.Combine for file paths** — never string concatenation
