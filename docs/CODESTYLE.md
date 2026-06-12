# CODESTYLE.md

## This is law. No exceptions.

---

## 1. Naming

**No abbreviations. Ever.** Names explain purpose without context.

### Variables and Fields
- Long, descriptive names: `currentAuthenticatedUser`, `totalCompletedExerciseCount`
- Booleans prefixed with `is`, `has`, `can`, `should`, `was`
- Collections — plural with content indicator: `unlockedSkillNodes`, `weeklyLeagueScoresByUserId`

### Methods
- Verb + full noun: `ProcessExerciseSubmission`, `FindUserByIdentifier`

### Classes
- Full role name: `ExerciseEvaluationService`, not `ExSvc`

### Interfaces
- `I` prefix + full name: `IExerciseRepository`

### Forbidden Abbreviations

| Forbidden | Correct |
|-----------|---------|
| `db` | `database` |
| `ctx` | `context` |
| `req` | `request` |
| `res` | `response` |
| `msg` | `message` |
| `btn` | `button` |
| `cfg` | `configuration` |
| `env` | `environment` |
| `auth` | `authentication` / `authorization` |
| `repo` | `repository` |
| `impl` | `implementation` |
| `svc` | `service` |
| `mgr` | `manager` |
| `util` | `utility` |
| `params` | `parameters` |
| `args` | `arguments` |
| `info` | `information` |
| `config` | `configuration` |
| `init` | `initialize` |
| `err` | `error` |
| `val` | `value` |
| `temp` | `temporary` |
| `num` | `number` |
| `str` | Use concrete name: `name`, `description`, `content` |
| `e` / `ex` | `exception` |
| `cb` | `callback` |
| `fn` | `function` |
| `i`, `j`, `k` | Meaningful names (only allowed in trivial 1-2 line loops) |
| `cmd` | `command` |
| `conn` | `connection` |
| `dest` | `destination` |
| `src` | `source` |
| `prop` | `property` |
| `attr` | `attribute` |
| `elem` | `element` |
| `idx` | `index` |
| `len` | `length` |
| `max` / `min` | `maximum` / `minimum` (in variable names) |

---

## 2. File Structure

**One class — one file. Always.**

File name matches the class inside exactly. No two classes, record+interface, or DTO+validator in one file.

### Backend (C#/.NET) — Feature-based Structure

```
src/
  Features/
    FeatureName/
      Endpoints/                    # Minimal API endpoints or Controllers
      Services/
        Abstract/                   # Interfaces (IUserService.cs)
        Implementation/             # Implementations (UserService.cs)
      Helpers/
        Abstract/
        Implementation/
      Models/                       # DTOs, request/response models
      Validators/                   # FluentValidation or custom validators
      Mappers/                      # Mapping profiles
      Constants/                    # Feature-specific constants
  Common/
    Constants/                      # Global constants (ErrorMessages.cs, RouteConstants.cs)
    Extensions/                     # Extension methods
    Middleware/
    Filters/
    Abstract/                       # Common interfaces (IRepository, IUnitOfWork)
    Implementation/
  Infrastructure/
    Database/
      Abstract/
      Implementation/
    Messaging/                      # RabbitMQ
      Abstract/
      Implementation/
    Storage/                        # MinIO
      Abstract/
      Implementation/
    Configuration/                  # Strongly-typed configurations
```

### Frontend (Next.js) Structure

```
src/
  features/
    featureName/
      components/
      hooks/
      services/                     # API clients
      constants/                    # String constants
      types/
      utils/
  shared/
    components/
    hooks/
    constants/
    types/
    utils/
    api/                            # Base API client, interceptors
  config/                           # Environment, feature flags
```

---

## 3. Required Patterns

### Service Layer
All business logic lives in services. Controller only receives request, calls service, returns result.

### Strategy
Different exercise types (multiple choice, free text, voice) are implemented as separate strategies with a common interface.

### Factory
Creation of complex objects (skill tree, personalized onboarding) via factories.

### Observer
Events after user actions (completed exercise -> update streak, update league, award XP) via observers.

### Custom Hook (Frontend)
All logic extracted from components into hooks. Component only renders.

---

## 4. Interfaces and Dependency Injection

### Every Service Must Have an Interface
- Interface in `Abstract/` folder: `IServiceName.cs`
- Implementation in `Implementation/` folder: `ServiceName.cs`
- All dependencies injected through interfaces, never concrete classes

### Class Modifiers
- `sealed` on all classes by default. Remove only if inheritance is actually needed.
- `internal` on implementations. `public` only on interfaces, DTOs, extension methods.
- Fields: `private readonly`. Access only through properties or methods.
- No `public` fields. Only `public` properties with `{ get; private set; }` or `{ get; init; }`.

### DI Registration
- Create extension method for each feature: `AddFeatureNameServices(this IServiceCollection services)`
- In `Program.cs` — only calls to these extension methods, no scattered `builder.Services.AddScoped<>()`
- Use correct lifetimes: `Scoped` for request-scoped services, `Singleton` for stateless, `Transient` only if necessary

### Example

```csharp
// Features/Payment/Services/Abstract/IPaymentService.cs
public interface IPaymentService
{
    Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default);
}

// Features/Payment/Services/Implementation/PaymentService.cs
internal sealed class PaymentService : IPaymentService
{
    private readonly IPaymentGateway _paymentGateway;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(IPaymentGateway paymentGateway, ILogger<PaymentService> logger)
    {
        _paymentGateway = paymentGateway;
        _logger = logger;
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        // ...
    }
}

// Features/Payment/PaymentServiceCollectionExtensions.cs
public static class PaymentServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentServices(this IServiceCollection services)
    {
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPaymentGateway, StripePaymentGateway>();
        return services;
    }
}
```

---

## 5. Magic Strings Prohibition

### All Strings Must Be Constants

Every string in code (route, error message, config key, header name, claim type, queue name, bucket name) must be in a static constants class.

### Backend Constants Structure

```csharp
// Common/Constants/RouteConstants.cs
public static class RouteConstants
{
    public const string ApiPrefix = "/api";
    public const string Version1 = "/v1";

    public static class Users
    {
        public const string Base = $"{ApiPrefix}{Version1}/users";
        public const string ById = $"{Base}/{{userId}}";
    }
}

// Common/Constants/ErrorMessages.cs
public static class ErrorMessages
{
    public const string UserNotFound = "User with the specified identifier was not found.";
    public const string InvalidPaymentAmount = "Payment amount must be greater than zero.";
}

// Common/Constants/ConfigurationKeys.cs
public static class ConfigurationKeys
{
    public const string DatabaseConnectionString = "Database:ConnectionString";
    public const string RabbitMessageQueueHost = "RabbitMessageQueue:Host";
}

// Common/Constants/MessageQueueNames.cs
public static class MessageQueueNames
{
    public const string UserRegistration = "user-registration";
    public const string PaymentProcessing = "payment-processing";
}
```

### Frontend Constants Structure

```typescript
// shared/constants/apiRoutes.ts
export const ApiRoutes = {
  users: {
    base: '/api/v1/users',
    byId: (userId: string) => `/api/v1/users/${userId}`,
  },
  payments: {
    base: '/api/v1/payments',
  },
} as const;

// shared/constants/errorMessages.ts
export const ErrorMessages = {
  userNotFound: 'User with the specified identifier was not found.',
  networkError: 'A network error occurred. Please try again.',
} as const;
```

---

## 6. C# Backend Code Style

### Modifiers and Types
- `sealed` on all classes by default
- `internal` on implementations, `public` on interfaces/DTOs
- `CancellationToken` in EVERY async method
- Nullable reference types enabled. No `null!` — if nullable, declare `?`
- `record` for DTOs and value objects where appropriate

### Error Handling
- Result pattern: `Result<T>` or specific exceptions, never `null` / `-1` as error signals
- Guard clauses at method start: `ArgumentNullException.ThrowIfNull()`, `ArgumentException.ThrowIfNullOrWhiteSpace()`

### Strings and Collections
- Avoid `string.Format()` — use string interpolation
- Return `IReadOnlyList<T>`, `IReadOnlyCollection<T>` instead of `List<T>`

### Logging
- Structured logging with `ILogger<T>`. No `Console.WriteLine()`
- Use `LoggerMessage.Define` or source-generated logging `[LoggerMessage]` for hot paths

---

## 7. Frontend Code Style (Next.js/TypeScript)

### TypeScript
- Strict mode enabled. No `any` — use concrete types or `unknown`
- Components — functional with named export: `export function ComponentName()`

### Architecture
- Custom hooks for business logic, components only render
- API calls through service layer (`features/featureName/services/`), not directly from components
- All env variables through typed config (`config/environment.ts`), not `process.env.XXX` directly

### Error Handling
- Global error boundary + typed errors

### Naming
- File names: `kebab-case`
- Components: `PascalCase`

---

## 8. Strongly-typed Configuration (C#)

Replace all `IConfiguration.GetValue<string>("Key")` with Options pattern:

```csharp
// Infrastructure/Configuration/DatabaseConfiguration.cs
public sealed class DatabaseConfiguration
{
    public const string SectionName = "Database";

    public required string ConnectionString { get; init; }
    public required string DatabaseName { get; init; }
    public int ConnectionTimeoutSeconds { get; init; } = 30;
}

// Registration:
services.Configure<DatabaseConfiguration>(
    configuration.GetSection(DatabaseConfiguration.SectionName));

// Usage — through IOptions<DatabaseConfiguration>, not IConfiguration
```

---

## 9. Comments

**Forbidden entirely.** If code requires a comment — rename the variable or decompose the method.

**No AI-generated comments — zero tolerance.** Do not leave any explanatory, narrating, or
"helpful" comments that an assistant tends to emit, e.g. `// Get the user`, `// Loop over
items`, `// TODO`, `// added by ...`, `// this method does X`, region banners, or
step-by-step `// 1. ... // 2. ...` annotations. This holds in every language (C#, TS, JS,
CSS, YAML). The only allowed exceptions are: license headers, and machine-required
directives that are not prose (e.g. `// eslint-disable-next-line <rule>` with a concrete
reason, `#pragma`, `<auto-generated>` in generated files). Self-document via names instead.

---

## 10. Git Commits

- Commit after EVERY change
- Format: `feat:`, `fix:`, `refactor:`, `docs:`, `test:`
- All commit messages in English
- Every commit = working state + updated documentation
- Never commit with failing tests
