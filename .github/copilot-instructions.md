# CampaignManager — Copilot Instructions

Call of Cthulhu 7e tabletop RPG manager. .NET 10 Blazor Server + PostgreSQL (JSONB-heavy) + Google OAuth + Tailwind CSS.

## Commands

```bash
dotnet run --project CampaignManager.Web          # Run app (preferred)
dotnet run --project CampaignManager.AppHost       # Run via .NET Aspire
dotnet build                                       # Build (Tailwind auto-compiles via MSBuild)

# Migrations — two separate contexts
dotnet ef migrations add <Name> --project CampaignManager.Web --context AppDbContext
dotnet ef migrations add <Name> --project CampaignManager.Web --context AppIdentityDbContext
dotnet ef database update --project CampaignManager.Web --context AppDbContext
```

No test projects exist. `TreatWarningsAsErrors` is enabled (except CS1591).

## Architecture

**Feature-based vertical slicing** — each domain in `Components/Features/{FeatureName}/`:
- `Components/` — Feature-specific UI components
- `Model/` (or `Models/`) — Domain models, DTOs, enums
- `Pages/` — Razor page views
- `Services/` — Business logic and data access

Features: Bestiary, Campaigns, Characters, Chase, Combat, Items, NPC, Scenarios, Skills, Spells, Weapons.

### Key Locations

| Path | Purpose |
|------|---------|
| `Program.cs` | DI, auth, middleware, SignalR config |
| `Utilities/DataBase/AppDbContext.cs` | Entity config, JSONB mappings (schema: `"games"`) |
| `Utilities/DataBase/AppIdentityDbContext.cs` | Identity schema (`"identity"`) |
| `Utilities/Services/CrudServiceHelper.cs` | Static CRUD helpers — eliminates boilerplate for `INamedEntity` entities |
| `Model/BaseDataBaseEntity.cs` | Base entity: `Id` (Guid v7), `CreatedAt`, `LastUpdated` — call `.Init()` on creation |
| `Extensions/EnumExtensions.cs` | Russian localization via `ToRussianString()` for enums |
| `Components/Shared/` | Reusable: Modal, ConfirmationModal, Badge, Button, Pagination, FilterPanel, LoadingIndicator, EmptyState, etc. |
| `wwwroot/css/design-system.css` | Custom design system (CSS variables, typography, colors) |
| `wwwroot/design-system-guide.md` | Design system documentation (in Russian) |

## Coding Conventions

### C# Style (Required)

- **`sealed` classes** for all services
- **Primary constructors** for DI — no traditional `_field` + constructor pattern
- **Collection expressions**: `return [];` not `new List<T>()`
- **Pattern matching**: `is null`, `is not null`
- **Structured logging**: `logger.LogError(ex, "Failed {Id}", id)` — never `$"interpolation"`
- **Nullable reference types** enabled throughout

### DbContext (Critical)

- **Always** use `IDbContextFactory<AppDbContext>` with `await using var db = await factory.CreateDbContextAsync()`
- **Never** store DbContext as a field — it is NOT thread-safe
- Two contexts: `AppDbContext` (app data) and `AppIdentityDbContext` (identity)

### Service Pattern

```csharp
public sealed class FeatureService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IdentityService identityService,
    ILogger<FeatureService> logger)
```

Register as scoped in `Program.cs`. For CRUD on `INamedEntity` entities, use static methods in `CrudServiceHelper` (handles caching, duplicate-name validation, `Init()` calls).

### JSONB Gotcha

Character data, creature characteristics, scenario items, spell alternative names, and skill metadata are all stored as JSONB columns. **Changing these model shapes can break deserialization of existing rows** — test against real data.

### UI Conventions

- **CSS isolation**: Always `*.razor.css` files — never inline `<style>` blocks
- **Tailwind CSS** with custom design system — see `wwwroot/design-system-guide.md`
- **`@rendermode InteractiveServer`** for all interactive components
- **Naming**: Pages → `{Entity}Page.razor`, Components → `{Entity}Card.razor`, Modals → `Add{Entity}Modal.razor`
- **Roles**: Player, GameMaster, Administrator — use `IdentityService.IsKeeper()` for GM/Admin checks

### SignalR Limits

Max message size: 2MB. Max buffered render batches: 20. Disconnected circuit retention: 3 minutes.
