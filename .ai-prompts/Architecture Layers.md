# Architecture Layers

## Solution Structure

Clean Architecture with 6 project layers. Each layer is a separate `.csproj`.

```
src/
├── ProjectName.Domain/           — Entities, Value Objects, Domain Events, Interfaces
├── ProjectName.Contracts/        — API DTOs, requests, responses, shared contract models
├── ProjectName.Application/      — Commands, Queries, Handlers, Validators, Interfaces
├── ProjectName.Infrastructure/   — Dapper Repositories, External Clients, Migrations, Config
├── ProjectName.Api/              — Controllers, Middleware, Filters, Program.cs
└── ProjectName.Desktop/          — Avalonia views, ViewModels, API clients, Converters, App.axaml

tests/
├── ProjectName.Domain.Tests/
├── ProjectName.Contracts.Tests/
├── ProjectName.Application.Tests/
├── ProjectName.Infrastructure.Tests/
├── ProjectName.Api.Tests/
└── ProjectName.Desktop.Tests/
```

## Project Reference Rules

```
Domain          → (nothing)
Contracts       → (nothing)
Application     → Domain, Contracts
Infrastructure  → Application, Domain, Contracts
Api             → Application, Infrastructure, Domain, Contracts
Desktop         → Contracts only
```

### Forbidden References

| From | To | Why |
|------|----|-----|
| Domain | Application | Domain is the core — knows nothing about use cases |
| Domain | Contracts | Domain is the core — knows nothing about transport contracts |
| Domain | Infrastructure | Domain is persistence-ignorant |
| Domain | Api | Domain knows nothing about HTTP |
| Domain | Desktop | Domain knows nothing about UI |
| Contracts | Any project | Contracts is a boundary project with zero dependencies |
| Application | Infrastructure | Application defines interfaces, Infrastructure implements |
| Application | Api | Application knows nothing about HTTP |
| Application | Desktop | Application knows nothing about UI |
| Desktop | Application | Desktop is an API client, not an in-process application host |
| Desktop | Domain | Desktop uses contract DTOs, never domain entities |
| Desktop | Infrastructure | Desktop must not access persistence or infrastructure implementations |

If a forbidden reference exists — **hard reject**.

### Verification

```xml
<!-- Domain.csproj — MUST have zero ProjectReference elements -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>
```

```xml
<!-- Contracts.csproj — MUST have zero ProjectReference elements -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>
```


## Avalonia 12 Project Rules

- Avalonia UI projects target `.NET 8` or later; prefer the current recommended target for new projects.
- All Avalonia package references must use the same latest `12.x` patch version. Do not mix `11.x` and `12.x` Avalonia packages.
- Use `.axaml` / `.axaml.cs` files (`App.axaml`, `MainWindow.axaml`) instead of WPF `.xaml` files.
- `App.axaml` must include a base theme in `Application.Styles` (`FluentTheme` by default) plus any `StyleInclude` files.
- Use Avalonia 12 built-in page navigation for Desktop navigation: `ContentPage`, `NavigationPage`, `DrawerPage`, `TabbedPage`, and related navigation controls. Custom navigation services may wrap these controls for MVVM boundaries, but must not reimplement a separate navigation stack, drawer shell, or page host when the built-in system covers the scenario.
- If the project explicitly uses Skia via `UseSkia()`, also configure text shaping with `UseHarfBuzz()` and reference `Avalonia.HarfBuzz`.
- Remove WPF-only APIs and packages: `System.Windows.*`, `DependencyProperty`, `BindingOperations`, `Application.DispatcherUnhandledException`, `OpenFileDialog`, `SaveFileDialog`, `RoutedCommand`.
- `Avalonia.Diagnostics` is removed in Avalonia 12; remove it and replace it with `AvaloniaUI.DiagnosticsSupport` only when the project explicitly opts into Avalonia Developer Tools.

## Layer Responsibilities

### Domain Layer
- **Contains:** Entities, Value Objects, Enums, Domain Events, Domain Exceptions, Repository Interfaces, Domain Services
- **Does NOT contain:** Data access, HTTP, UI, logging, configuration, or dependencies on outer application layers
- **Namespace:** `ProjectName.Domain.Entities`, `.ValueObjects`, `.Events`, `.Exceptions`, `.Interfaces`

### Contracts Layer
- **Contains:** DTOs, API request records, API response records, pagination/result contract models used across Api and Desktop
- **Does NOT contain:** Domain entities, Value Objects with domain behavior, Application handlers, validators, MediatR requests, repository interfaces, SQL, HTTP controllers, Avalonia types
- **NuGet packages:** NONE unless serialization attributes are explicitly required
- **Namespace:** `ProjectName.Contracts.{FeatureName}` or `ProjectName.Contracts.{FeatureName}.Requests`, `.Responses`, `.Dtos`
- **References:** NONE

Contracts types are serialization-safe and behavior-free.

### Application Layer
- **Contains:** Commands, Queries, Handlers, Validators, Application Interfaces, Pipeline Behaviors, Mapping logic
- **Does NOT contain:** Data access implementation, HTTP controllers, UI, SQL queries, API request/response contracts consumed by external clients
- **NuGet packages:** MediatR, FluentValidation
- **Namespace:** `ProjectName.Application.Features.{FeatureName}.Commands`, `.Queries`, `.Validators`, `.Mappings`

### Infrastructure Layer
- **Contains:** Dapper repositories, `IDbConnectionFactory`, external service clients, email/notification services, file storage, migrations
- **Does NOT contain:** Business logic, HTTP controllers, UI
- **NuGet packages:** Dapper, SqlClient/Npgsql, Polly
- **Namespace:** `ProjectName.Infrastructure.Persistence`, `.Services`, `.Migrations`

### Api Layer
- **Contains:** Controllers, `Program.cs` (DI composition root), middleware, exception filters, Swagger, auth config
- **Does NOT contain:** Business logic, SQL queries, direct DB access, domain behavior in controllers/middleware/filters, Domain namespace/type usage outside `Program.cs`
- **Composition root rule:** `Program.cs` is allowed to reference `Domain` and must register `AddDomainServices()` explicitly. This is the only Api-layer place where Domain service registration belongs.
- **Namespace:** `ProjectName.Api.Controllers`, `.Middleware`, `.Filters`

### Desktop Avalonia Layer
- **Contains:** Avalonia views (`.axaml`), ViewModels, Commands, Converters, Navigation, API clients, `App.axaml`, UI-only services
- **Does NOT contain by default:** Business logic, SQL queries, direct DB access, domain entities, Application handlers, Application commands/queries, references to Application, Domain, or Infrastructure
- **Folders strictly by role:** `Views/`, `ViewModels/`, `Services/`, `Converters/`, `Helpers/`, `Controls/`
- **Namespace:** `ProjectName.Desktop.Views`, `.ViewModels`, `.Services`

Desktop uses DTOs, requests, and responses from `ProjectName.Contracts` only.

#### Client-Side Business Logic Exception

Desktop may contain limited client-side business logic only when all conditions are true:

1. The feature request or approved design explicitly documents this as an architectural assumption.
2. The reason is operational, such as reducing server load, avoiding excessive API chatter, supporting responsive previews, or enabling offline/latency-tolerant UI behavior.
3. The logic uses only data already available through `ProjectName.Contracts`, local UI state, or client-owned settings.
4. The logic is not the source of truth for persistence, authorization, billing, server validation, paid execution, or irreversible business decisions.
5. The authoritative rule still lives in Domain/Application or is enforced by the server when correctness matters.
6. The design documents the boundary of the exception and includes tests proving the client behavior stays within that boundary.

Allowed examples include deterministic previews, local estimates, client-side eligibility hints, draft calculations, and UI-only projections that avoid repeated server calls. Forbidden examples remain: authorization decisions, payment settlement, final validation, persistence rules, direct use of domain entities, direct calls into Application/Domain/Infrastructure, SQL, and external provider calls that belong on the server.

## Dependency Inversion

Interfaces live in the layer that USES them, implementations in the layer that KNOWS HOW:

| Interface | Defined in | Implemented in |
|-----------|-----------|----------------|
| `IUserRepository` | Domain or Application | Infrastructure |
| `ICurrentUserService` | Application | Api (reads HttpContext) |
| `IDateTimeProvider` | Application | Infrastructure |
| `IEmailService` | Application | Infrastructure |
| `IDbConnectionFactory` | Application or Infrastructure | Infrastructure |
| `IXxxApiClient` | Desktop | Desktop (HttpClient wrapper) |
| `INavigationService` | Desktop | Desktop |

### DIP Review Guidance

DIP is about architectural boundaries and volatile details. It does not require an interface for every concrete class.

Raise a DIP finding only when a higher-level policy depends on a lower-level or volatile detail:

- Domain/Application depends on Infrastructure, Api, Desktop, framework, database, file system, HTTP, or UI details.
- ViewModels depend directly on Avalonia controls, Dispatcher, windows, dialogs, or storage providers.
- Business/application services depend on concrete external clients, repositories, clocks, file systems, random generators, or environment access.
- A stable component depends on a volatile component across a project, layer, or feature boundary.

Do not reject by DIP alone:

- Concrete collaborators inside the same cohesive implementation component.
- UI control infrastructure classes inside `Desktop/Controls/*` that are composed together and not exposed as architectural ports.
- Concrete value objects, DTOs, options, messages, commands, records, and framework-mandated types.
- Composition root code that creates or registers concrete implementations.
- Existing concrete dependencies that were not introduced or worsened by the reviewed change.

Introduce an abstraction only when it protects a higher-level policy from a lower-level detail, crosses a layer/component boundary, removes a cycle, supports multiple implementations, or makes meaningful testing possible.

Before raising a DIP finding, explain:

1. Which higher-level policy is being protected.
2. Which lower-level detail is volatile.
3. Which architectural boundary is crossed.
4. What concrete change risk the abstraction would reduce.

## DI Composition Root

All registrations happen in `Program.cs` (Api) or `App.axaml.cs` / application bootstrap code (Desktop). Each layer exposes a single `IServiceCollection` extension method.

`Program.cs` is the composition root for the API process. It explicitly references and registers every server layer. Do not hide Domain registration inside Application or Infrastructure to avoid an Api project reference to Domain; that makes the object graph implicit and violates the composition root rule.

```csharp
// Program.cs
builder.Services.AddDomainServices();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
```

### DI Lifetime Rules

- Singleton must NOT depend on Scoped or Transient — captive dependency bug.
- Service Locator pattern is FORBIDDEN inside classes. `IServiceProvider` only at composition root.
- Do not instantiate long-lived services, external dependencies, infrastructure adapters, repositories, API clients, clocks, file systems, or other replaceable dependencies with `new` inside business/application classes. Receive them through constructor injection.
- `new` is allowed for values, DTOs, commands, operations, UI controls, short-lived owned helpers, and internal implementation objects that do not cross architectural boundaries and are not intended to be replaced through DI.

## SOLID Rules

- Each class has a single responsibility (SRP).
- Depend on abstractions at architectural boundaries and when the dependency is volatile, external, replaceable, or owned by another layer (DIP).
- Do not create one-method interfaces only to satisfy DIP. An interface must represent a stable port, a replaceable dependency, or a real boundary.
- Method and class length thresholds are recommendations, not blockers by themselves.
- Treat methods over ~20–30 lines and classes over ~200–300 lines as prompts to check readability, SRP, and cohesion.
- Extract or decompose only when it produces a concrete benefit: clearer responsibility boundaries, lower duplication, easier testing, or simpler control flow.
- Do not split code only to satisfy a line-count target.

## Knowledge Duplication Rules

Duplicating project-owned knowledge across files is an architecture issue when the repeated value, key, string, metadata, mapping rule, validation rule, branching condition, or option definition must change consistently.

Do not repeat the same project-owned knowledge in multiple classes, layers, views, handlers, repositories, services, or configuration points. Give the knowledge one owner and reference that owner from consumers.

Treat these as duplicated knowledge when they appear in multiple places with the same meaning:

- Constants, static readonly values, keys, route segments, option names, error codes, permission names, feature flags, and setting names.
- User-facing strings that must stay consistent across screens, handlers, validation, logs, API responses, or tests.
- Resource keys, design values, style metadata, layout constants, and localization keys.
- Validation limits, validation messages, business rule thresholds, and state transition conditions.
- Mapping rules between Domain, Contracts, persistence rows, API models, ViewModels, and UI models.
- Metadata for fields, settings, filters, commands, columns, actions, permissions, variants, or supported modes.
- `switch` / `if-else` branches that encode the same variant list in multiple consumers.

Use the narrowest correct owner:

- Domain entity, value object, enum, specification, or domain service for domain concepts, invariants, state transitions, and business thresholds.
- Contracts DTO, enum, request, response, or shared contract metadata for transport shape shared by Api and Desktop.
- Application mapping/helper, behavior, options, registry, definition, or strategy for application-owned coordination and use-case metadata.
- Infrastructure-private constant only for repository-private SQL, persistence names, or adapter implementation details that do not cross a boundary.
- Desktop resource, localization entry, style, control theme, ViewModel helper, or UI definition for UI-owned values and presentation metadata.
- Test helper or deterministic test constant for repeated test setup data that is not production knowledge.

Do not extract coincidental duplicates. Repetition is allowed when values only look the same but have different owners, different reasons to change, or are private one-off implementation details.

Reviewer rule: if a change introduces, preserves, or expands duplicated project-owned knowledge across 2+ files, report it unless the duplication is explicitly justified by different ownership or different change reasons. A finding must cite at least two duplicated locations and name the correct owner.

## Refactoring Rules

- NEVER change behaviour — only structure.
- Apply: extract method / extract class, rename with all usages, collapse duplicates into shared helper, simplify nested conditionals, remove dead code.

## Rules Summary

1. **Domain has ZERO outward project/layer dependencies** — no project references
2. **Contracts has ZERO dependencies** — behavior-free DTOs, requests, responses
3. **Application depends only on Domain and Contracts** — never on Infrastructure, Api, or Desktop
4. **Infrastructure implements interfaces from Application/Domain**
5. **Controllers contain no business logic** — only MediatR dispatch
6. **Each layer has its own DI extension method, and Api `Program.cs` calls server-layer DI methods explicitly**
7. **No circular references**
8. **Desktop/Avalonia references Contracts only** — never Application, Domain, or Infrastructure
9. **Singleton must not depend on Scoped/Transient**
10. **No Service Locator — constructor injection for replaceable services and external dependencies**
11. **Method/class size thresholds are recommendations** — split only when it improves readability, SRP, testing, or maintainability
12. **No duplicated project-owned knowledge** — values, keys, strings, metadata, rules, mappings, and variant lists that must change consistently have one owner
