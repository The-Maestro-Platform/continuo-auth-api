# Agent Development Guide — Auth API

## Service Overview

> **HIGH-RISK SERVICE** — Changes to auth-api require 2 human approvals and comprehensive tests before merge. This service controls authentication, authorization, and tenant identity for the entire platform.

**Domain**: Authentication, authorization, user management, tenant management, roles, and permissions.
**Service Name**: `auth-api`
**Database**: `ContinuoAuthDb`
**Schema Prefix**: `aut.`

### Key Models
- `Credential` — Login credentials (username/password hashes, lockout tracking)
- `PlatformUser` — Platform-level user accounts (admins, operators)
- `TenantUser` — Tenant-scoped user accounts (staff within a specific tenant)
- `Customer` — End-customer accounts (loyalty, ordering)
- `Role` — Role definitions (PlatformOwner, TenantAdmin, Staff, etc.)
- `Permission` — Granular permissions assigned to roles
- `Tenant` — Tenant entity for multi-tenant isolation

### Key Endpoints
- Login / token issuance (JWT)
- User CRUD (platform users, tenant users, customers)
- Role and permission management
- Tenant provisioning and configuration
- Screen/menu assignments for role-based UI access
- Connection string management (used by security-api pattern)

### Service-Specific Rules
- **Password hashing**: Always use the established hashing mechanism — never store plaintext or use custom hashing
- **JWT token generation**: Token claims must include tenant context, user ID, roles, and permissions
- **Audit logging**: ALL write operations (create, update, delete) on credentials, roles, and permissions MUST be audit-logged
- **BFF auth handler**: Returns 403 (not 401) with localized message "Gecersiz kullanici adi veya sifre" for failed logins via BFF
- **Screen assignments**: maestro-console and other admin UIs require explicit screen assignments in Platform Roller & Ekranlar
- **Tenant isolation is critical**: User queries MUST always scope by tenant — a TenantUser from Tenant A must NEVER see data from Tenant B
- **Rate limiting**: Login endpoints should be protected against brute force (lockout after N failed attempts)
- **Token refresh**: Refresh tokens must be stored securely and rotated on use
- **Connection string resolution**: security-api caches connection strings in memory — must restart pod after DB updates to `sec.SecurityConnectionStrings`

### Security Considerations (Beyond Standard Rules)
- Any change to authentication flow, token generation, or permission checks is extremely high-risk
- Never log passwords, tokens, or secrets — even at Debug level
- Password reset flows must use time-limited, single-use tokens
- Role hierarchy must be enforced — a TenantAdmin cannot grant PlatformOwner permissions
- All admin endpoints must require PlatformOwner or equivalent role

---

## Adding a New UI Screen End-to-End

> Use this checklist when introducing a new screen in any client app (`maestro-console`, `console-admin`, `continuo-ops-ui`, etc.). Skipping a step typically results in: screen not in nav menu, "Permission'lara yazınca hiç bir şey gelmiyor" in Roller & Ekranlar, or backend endpoints returning 403/401 even though the user "should" have access. All four pieces must land together.

### Step 1 — Frontend route + component
- Create the page under `ui/apps/<app-name>/app/<route-path>/page.tsx` (Next.js App Router).
- Place the component in `ui/apps/<app-name>/components/<feature>/<component>.tsx`.
- If the screen lives inside `/workspaces/*` it inherits the workspace layout (toolbar, user menu); a sibling route at `/profile` does NOT — keep auxiliary screens under `/workspaces` for consistent chrome.

### Step 2 — Permission keys (backend)
- Edit [services/auth-api/Permissions/PermissionKeys.cs](Permissions/PermissionKeys.cs) — add `public const string XxxYyy = "platform.feature.action";` constants. Use `platform.*` for platform-scope, `tenant.*` for tenant-scope, `ops.*` for ops UI permissions.
- Edit [services/auth-api/Permissions/PermissionCatalog.cs](Permissions/PermissionCatalog.cs) — add `new(PermissionKeys.Platform.XxxYyy, "Display Name", "User-facing description shown in Roller & Ekranlar.", RoleScope.Platform),` to the `PlatformOnly`/`TenantOnly` array. **Without a catalog entry the permission never appears in the operations console permission picker — `mae` autocomplete returns empty.**

### Step 3 — Permission keys (frontend)
- Edit `ui/apps/<app-name>/lib/access.ts` — mirror the backend keys as `export const XXX_YYY = 'platform.feature.action';` constants. Group multiple related keys into a `XXX_PERMISSIONS = [...]` array for screen-level any-of guards.
- Reference these constants from the `requiredPermissions` field of the nav item in `app/workspaces/layout.tsx` (or the equivalent layout).
- Front-end constants and back-end constants are two halves of the same contract — keep their values byte-identical.

### Step 4 — Screen registration in seed
- Edit [services/auth-api/Seed/AuthSeeder.cs](Seed/AuthSeeder.cs) — add a `SeedScreen` row to the `Screens` array. Format:
  ```csharp
  new("maestro-console",            // appName — must match resolveClientApp()
      "/workspaces/dev/feature",        // route key (also displayed in admin grid)
      "Feature Display Name",           // shown in admin grid + nav fallback
      "Tek cümlelik Türkçe açıklama.",  // tooltip / catalog description
      new[] { "platform.feature.use" }, // permissions that grant screen access
      "/workspaces/dev/feature",        // path for the link
      "icon-name",                      // lucide icon ID (sparkles, user, server)
      "Developer",                      // category label
      250),                             // sort order within category
  ```
- Without this row the screen is not picked up by the "Bu uygulamadaki tüm ekranları ekle" admin bulk action; it never appears in the role's screen grid.

### Step 5 — Default role assignments (optional)
- If you want a baseline role to receive the new permission out-of-the-box, edit `SeedRole` arrays at the top of `AuthSeeder.cs` (PlatformDev, PlatformAdmin, etc.) and append the new key to its permission list.
- `PlatformOwner` is auto-assigned all platform permissions via `PermissionCatalog.All.Where(...)` so it does NOT need a manual edit.

### Step 6 — Migration regeneration
- Catalog data is `HasData`-seeded into `Permission` table via the EF model snapshot. Adding a new `PermissionDefinition` requires `dotnet ef migrations add AddXxxPermission --context AuthDbContext`. **Otherwise the migration runner sees no schema delta and the new keys never reach the DB.**
- Run from `services/auth-api/`. Make sure auth-api is NOT running (DLL lock prevents migration generation).
- Same for screen seeding — the `Screen` rows live in `AuthSeeder.SeedAsync`, which runs on every startup; no migration needed for screen additions, but a service restart IS required so the new row gets upserted.

### Step 7 — Backend endpoint protection
- New endpoints exposing the feature must carry `RequireAuthorization(MyPolicy)` where the policy was registered with `AddAuthorization(o => o.AddPolicy(...))` in the service's `Program.cs`.
- Permission policies should match the catalog keys — see [services/maestro-api/Infrastructure/MaestroPermissions.cs](../maestro-api/Infrastructure/MaestroPermissions.cs) for the recommended pattern (claims `permission` claim type + owner-login bypass).
- Use `IAsyncAuthorizationFilter` MVC attribute `[RequireAnyPermission("a", "b")]` for controllers (security-api pattern), or minimal-API `RequireAuthorization("PolicyName")` for endpoint groups.

### Step 8 — Login token refresh
- After deployment, **users must logout/login** to pick up the new permission claims in their JWT. Existing tokens were issued before the new permission existed; nav menu and endpoints will keep showing 403/missing entries until token refresh.

### Common Failure Symptoms
| Symptom | Step you skipped |
| --- | --- |
| "mae yazınca hiçbir şey gelmiyor" in Roller & Ekranlar | Step 2 — PermissionCatalog entry missing |
| Screen not in admin grid for the app | Step 4 — SeedScreen entry missing |
| Endpoint returns 403 even after granting | Step 6 — migration not generated, or Step 8 — user has stale token |
| Nav menu hides screen for everyone (including PlatformOwner) | Step 3 — frontend constant typo / wrong array reference |
| 401 instead of 403 from endpoint | Step 7 — endpoint did not call `RequireAuthorization` at all |

### Reference Implementation
The Maestro AI screen end-to-end PR shows all eight steps in one change set: search the repo for `platform.maestro.use` to see every touchpoint at once.

---

## Platform Architecture & Conventions

### Service Bootstrap
- All services use `Bootstrap.CreateBuilder(args, serviceName)` and `Bootstrap.CreateApp(builder, serviceName)` from `Continuo.Observability`
- Target framework: .NET 10 (`net10.0`), nullable enabled, implicit usings enabled
- Services reference building blocks: `Continuo.Shared`, `Continuo.Observability`, `Continuo.Messaging`, `Continuo.Persistence`

### Database & Persistence
- Use `AddContinuoPersistence(config, serviceName)` for base DB context
- Use `AddServiceDbContext<TDbContext>(config, serviceName)` for service-specific DbContext
- Connection strings resolve via `PersistenceExtensions.ResolveConnectionString()` — in containers, security-api is checked first
- DB naming convention: `Continuo{ServiceName}Db` (e.g., `ContinuoOrderDb`)
- Primary keys: ULID string `nvarchar(26)` — never use auto-increment int or GUID
- Use `ContinuoDbContext` as base class for all DbContexts
- Outbox pattern: `dbo.OutboxMessages` table for reliable event publishing
- Always use `CreatedAtUtc` (DateTime, datetime2) for timestamps — UTC only
- Use schema prefixes per service (e.g., `ord.`, `aut.`, `cam.`)
- SQL Server compatibility level 120 (no OPENJSON) — use `UseCompatibilityLevel(120)`
- Enable retry on failure: `sqlOptions.EnableRetryOnFailure()`
- Migrations: `MigrationRunner.ApplyMigrations<TDbContext>(app.Services, serviceName, ensureCreatedFallback: true)`
- ParameterDefinitions table: use `IParameterProvider` for configuration values, not hardcoded constants

### Messaging (RabbitMQ + MassTransit)
- Use `AddContinuoMessaging(config, serviceName)` or inline `AddMassTransit` with `ConfigureRabbitMq(config, serviceName)`
- Event contracts live in `Continuo.Shared.Contracts` — reuse existing events before creating new ones
- Context propagation is automatic via MassTransit filters (`tc-tenant-code`, `tc-correlation-id`, etc.)
- Outbox pattern ensures messages are published only after DB transaction commits
- Consumer naming: `{EventName}Consumer` in a `Consumers/` folder

### API Design
- Use Minimal API (`app.MapGet/MapPost/...`) or Controllers — follow the existing pattern in the service
- All endpoints exposed via gateway must have `[ContinuoProxyMethod]` attribute
- HTML-accepting endpoints must have `[ContinuoProxyUiHtmlAttribute]`
- Call `app.UpdateEndpointProxyFromRoutes(serviceName, baseUrl: null, version: "v1")` before `app.Run()`
- Return `Result<T>` for operation results, `PagedResult<T>` for lists
- Use `Paging.NormalizePageSize()` for pagination
- Error responses: use the common `ErrorDto` / `ErrorResponse` model
- Health endpoint: already mapped at `/health` and `/healthz` by Bootstrap

### Authentication & Authorization
- JWT Bearer auth with `JWT:SECRET`, `JWT:ISSUER`, `JWT:AUDIENCE` from config
- Use `[Authorize]` attribute or `RequireAuthorization()` for protected endpoints
- Tenant context: `AddTenantServices()` + `app.UseTenantMiddleware()` — `ITenantContext` is injected via DI
- Platform endpoints (admin, health, swagger) skip tenant validation
- Context headers propagated: `X-Tenant-Slug`, `X-Correlation-Id`, `X-User-Id`, `X-Client-App`, etc.

### Service-to-Service Communication
- Use `ServiceCallExecutor` for HTTP calls between services (saga support with compensation)
- Use named HttpClients via `AddAppCodeHttpClient("appCode", "serviceName")`
- Gateway proxy: `AddContinuoGatewayProxy(config)` — never call services directly from UI
- Service discovery: `AddContinuoServiceDiscovery(config, serviceName)`

### Observability
- Serilog for logging (auto-configured by Bootstrap)
- OpenTelemetry tracing with activity source `continuo-{serviceName}`
- Error logging: `ErrorLogs` table (DB mode) or file
- Request logging: `ApiRequests` table or file
- Use structured logging: `Log.Information("{Service} processed {Count} items", serviceName, count)`

### Code Quality & SOLID
- **Single Responsibility**: Each service owns ONE bounded context. Do not leak domain logic across services.
- **Open/Closed**: Use the building blocks extension methods — extend, don't modify base classes.
- **Liskov Substitution**: DbContext classes must extend `ContinuoDbContext`. Service interfaces must be substitutable.
- **Interface Segregation**: Define small, focused interfaces (e.g., `IOrderService`, `IPaymentGateway`).
- **Dependency Inversion**: Always inject via DI. Never `new` up services or DbContexts manually.
- Keep controllers/endpoints thin — business logic belongs in `Services/` folder.
- Models in `Models/` folder, data access in `Data/` folder.
- Use records for DTOs and events.

### Security Rules
- NEVER hardcode secrets, connection strings, or API keys — use configuration/env vars
- All user input must be validated before processing
- SQL injection prevention: ALWAYS use parameterized queries or EF Core — never string concatenation in SQL
- XSS: endpoints accepting HTML must use `[ContinuoProxyUiHtmlAttribute]` and ingress XSS guard runs automatically
- Tenant isolation: ALWAYS filter data by tenant context — never return cross-tenant data
- Auth endpoints (auth-api, payment-api, wallet-api, tenant-api) are HIGH-RISK — require 2 human approvals + tests
- CSRF: Gateway handles double-submit cookie pattern — services behind gateway are protected

### Performance Rules
- Use async/await throughout — never `.Result` or `.Wait()` (except in `ResolveConnectionString` bootstrap)
- Enable EF Core retry on failure for transient errors
- Use `Paging.NormalizePageSize()` — never return unbounded result sets
- Cache parameter values via `IParameterProvider` (memory → Redis → DB hierarchy)
- Keep DB transactions short — use outbox for async event publishing
- Use optimistic concurrency (RowVersion) for critical tables
- Bulk operations: prefer batch inserts/updates over individual operations

### Testing
- Unit tests: service logic in isolation
- Integration tests: hit real DB context, verify migrations
- Test project naming: `{ServiceName}.Api.Tests` under `tests/`
- Use `public partial class Program;` at end of Program.cs for WebApplicationFactory integration tests
