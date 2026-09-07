# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CampaignManager is a tabletop RPG (Call of Cthulhu 7e) management system built with .NET 10 Blazor Server. It manages
campaigns, characters, scenarios, and game assets (creatures, items, weapons, spells, skills). Uses PostgreSQL with
Entity Framework Core, Google OAuth authentication, and .NET Aspire for orchestration.

## Development Commands

```bash
# Run the web application (preferred)
dotnet run --project CampaignManager.Web

# Run via .NET Aspire AppHost
dotnet run --project CampaignManager.AppHost

# Build
dotnet build

# Database migrations (two separate contexts)
dotnet ef migrations add <Name> --project CampaignManager.Web --context AppDbContext
dotnet ef migrations add <Name> --project CampaignManager.Web --context AppIdentityDbContext
dotnet ef database update --project CampaignManager.Web --context AppDbContext
dotnet ef database update --project CampaignManager.Web --context AppIdentityDbContext
```

**Tailwind CSS** is built automatically via MSBuild targets in the csproj:
- Debug: `npx tailwindcss@3 -i ./Styles/tailwind.css -o ./wwwroot/styles.css`
- Release: same with `--minify`

**No test projects exist** in this solution.

## Architecture

### Solution Structure

Solution file is `CampaignManager.slnx` (new XML format).

### Feature-Based Vertical Slicing

Each domain lives in `CampaignManager.Web/Components/Features/{FeatureName}/` with subdirectories:
- `Components/` — Feature-specific UI components
- `Model/` (or `Models/`) — Domain models and DTOs
- `Pages/` — Full Razor page views
- `Services/` — Business logic and data access

Each feature folder has its own `CLAUDE.md` with that feature's services, models, and gotchas (entity relationships, cross-feature dependencies, deviations from the patterns below) — see "Documentation Conventions" below.

### Data Architecture

- **Two DbContexts**: `AppDbContext` (schema: "games") for app data, `AppIdentityDbContext` (schema: "identity") for auth
- **Base Entity**: All entities inherit `BaseDataBaseEntity` with `Id` (Guid v7), `CreatedAt`, `LastUpdated` — call `.Init()` on creation
- **JSONB heavily used**: Character stats, creature characteristics, scenario data stored as JSONB columns. Be careful when changing model shapes — JSONB serialization is sensitive to schema changes.
- **Factory pattern**: Always use `IDbContextFactory<AppDbContext>` with `await using var dbContext = await dbContextFactory.CreateDbContextAsync()` — DbContext is NOT thread-safe

### API Endpoints

Uses minimal APIs (not controllers), mapped in `Utilities/Api/`:
- `AccountEndpoints.cs` — `/api/account/login`, `/api/account/logout`
- `MinioApi.cs` — File storage
- `CharacterMigrationApi.cs` — Character migration

Swagger available at `/swagger`.

## Code Patterns

### Service Pattern (Required)

All services must be `sealed`, use primary constructors, and follow this template:

```csharp
public sealed class FeatureService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IdentityService identityService,
    ILogger<FeatureService> logger)
{
    public async Task<List<Entity>> GetAllAsync()
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var userId = identityService.GetCurrentUserId();
        return await dbContext.Entities
            .Where(e => e.UserId == userId)
            .ToListAsync();
    }
}
```

Register as scoped in `Program.cs`: `builder.Services.AddScoped<FeatureService>();`

Reference-data services (catalog features like Items, Skills, Spells, Weapons, Books, Bestiary) additionally inject `IMemoryCache cache` for read caching — follow that convention for new catalog-style services. Runtime resolution engines (`Combat/Services/CombatService`, `Chase/Services/ChaseService`) deliberately break this pattern — they're stateful, non-DI session services, not persistence CRUD; see their feature `CLAUDE.md` files before copying their shape elsewhere.

### C# Conventions

- **Primary constructors** for dependency injection (no traditional constructor + field pattern)
- **Collection expressions**: `return [];` instead of `new List<T>()`
- **Pattern matching**: `if (x is null)`, `if (x is not null)`
- **Nullable reference types** enabled throughout
- **Structured logging**: `logger.LogError(ex, "Error loading {SkillId}", id)` — never string interpolation
- **`TreatWarningsAsErrors`** is enabled (except CS1591 for missing XML docs)

### UI Patterns

- All interactive components use `@rendermode InteractiveServer`
- **CSS isolation**: Always use `*.razor.css` files for component-scoped styles, never inline `<style>` blocks
- Tailwind CSS with custom design system in `wwwroot/css/design-system.css`
- Design system guide (in Russian) at `wwwroot/design-system-guide.md`
- Shared components in `Components/Shared/`: Badge, Modal, ConfirmationModal, NotificationAlert, SaveButton, Pagination, FilterPanel, LoadingIndicator, EmptyState, etc.

### Target device: iPad Pro M2

**The primary device for this app is an iPad Pro M2 — it's what the Keeper actually runs at the table.**
Any UI change must be checked at that viewport in the browser preview before it's called done, not only
at desktop width:

- **Landscape 1366×1024** — the main orientation at the table. Check this one first.
- **Portrait 1024×1366** — Tailwind's `lg:` breakpoint is exactly 1024px, so a three-column
  `lg:col-span-*` grid switches to its widest layout right at portrait width and columns get very tight.
  Verify nothing clips, and that no control ends up narrower than a comfortable tap target.

Practical rules that follow from it:
- Tap targets: buttons and checkboxes need real padding — `cm-btn-sm` is the floor, not `text-xs` bare links.
- Never rely on `title=` tooltips to carry information: there's no hover on a touch screen.
- Wide content (tracks, tables, timelines) scrolls inside its own `overflow-x-auto` container so the page
  body never scrolls sideways.
- Prefer `flex-wrap` on button rows — an unwrapped row of six actions overflows in portrait.

`mcp__Claude_Browser__resize_window` with `{width: 1366, height: 1024}` (and then `1024×1366`) is how to
check this; reset with `preset: "desktop"` when done.

### Design System Colors

- **Primary**: Slate/graphite gray (#64748B) — headings, nav, buttons
- **Secondary**: Warm stone brown (#78716C) — backgrounds, accents
- **Accent**: Muted steel blue (#4B7FAF) — highlights, badges, info
- **Status**: Success (#2C9D49), Warning (#D97706), Error (#C71D20)
- **Fonts**: Inter (primary), Bookman Old Style (serif headings), JetBrains Mono (code)

### Naming Conventions

- Pages: `{Entity}Page.razor`, `{Entity}EditPage.razor`
- Components: `{Entity}Card.razor`, `{Entity}Form.razor`, `{Entity}List.razor`
- Modals: `Add{Entity}Modal.razor`, `Edit{Entity}Modal.razor`

## Configuration

- `ConnectionStrings:DefaultConnection` — PostgreSQL
- `Authentication:Google:ClientId` / `ClientSecret` — Google OAuth
- Npgsql configured with dynamic JSON support and legacy timestamp behavior
- SignalR: 2MB message size limit, 15-buffer capacity, 30s handshake timeout
- Blazor Server: 20 max buffered render batches

## External Services

- **Russian localization**: `EnumExtensions.ToRussianString()` for weapon types, creature types, skill categories

## Key Files

- `CampaignManager.Web/Program.cs` — DI, auth, middleware, SignalR config
- `CampaignManager.Web/Utilities/DataBase/AppDbContext.cs` — Entity configuration, JSONB mappings
- `CampaignManager.Web/Utilities/DataBase/AppIdentityDbContext.cs` — Identity schema
- `CampaignManager.Web/Model/BaseDataBaseEntity.cs` — Base entity with Guid v7
- `CampaignManager.Web/Components/_Imports.razor` — Global using directives
- `CampaignManager.Web/Components/Features/` — All feature vertical slices

## Documentation Conventions

This file is for conventions and patterns that apply across the whole app. **Feature-specific knowledge (a feature's services, models, entity relationships, cross-feature dependencies, or deviations from the patterns above) belongs in that feature's own `Components/Features/{FeatureName}/CLAUDE.md`, not here.**

- Every feature under `Components/Features/` has a `CLAUDE.md` — it loads automatically only when you're working with files under that feature's directory.
- Adding a feature-specific gotcha, model, or service to this root file instead of the feature's own file is the failure mode this rule exists to prevent — it bloats every session's context regardless of which feature is being touched.
- When a feature gains a new service, model, or non-obvious relationship, update that feature's `CLAUDE.md`, not this one. Create the feature's `CLAUDE.md` if it doesn't exist yet.
- When adding a brand-new feature folder, give it a `CLAUDE.md` from the start.
