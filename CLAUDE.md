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

- **CampaignManager.Web** — Main Blazor Server application
- **CampaignManager.AppHost** — .NET Aspire orchestration (simple project reference)
- **CampaignManager.ServiceDefaults** — Shared service config (OpenTelemetry, health checks, resilience)

Solution file is `CampaignManager.slnx` (new XML format).

### Feature-Based Vertical Slicing

Each domain lives in `CampaignManager.Web/Components/Features/{FeatureName}/` with subdirectories:
- `Components/` — Feature-specific UI components
- `Model/` (or `Models/`) — Domain models and DTOs
- `Pages/` — Full Razor page views
- `Services/` — Business logic and data access

Features: Bestiary, Campaigns, Characters, Chase, Combat, Items, NPC, Scenarios, Skills, Spells, Weapons.

### Data Architecture

- **Two DbContexts**: `AppDbContext` (schema: "games") for app data, `AppIdentityDbContext` (schema: "identity") for auth
- **Base Entity**: All entities inherit `BaseDataBaseEntity` with `Id` (Guid v7), `CreatedAt`, `LastUpdated` — call `.Init()` on creation
- **JSONB heavily used**: Character stats, creature characteristics, scenario data stored as JSONB columns. Be careful when changing model shapes — JSONB serialization is sensitive to schema changes.
- **Factory pattern**: Always use `IDbContextFactory<AppDbContext>` with `await using var dbContext = await dbContextFactory.CreateDbContextAsync()` — DbContext is NOT thread-safe

### Key Entity Relationships

- **Campaign** → (N) **CampaignPlayer** → (N) **CharacterStorageDto** (character data as JSONB)
- **Scenario** links to Campaign and contains NPCs via CharacterStorageDto
- Independent entities: Creature, Item, Weapon, Spell, SkillModel

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

- **Minio**: Object storage for file management
- **QuestPDF**: PDF generation for character sheets
- **Markdig**: Markdown processing
- **Russian localization**: `EnumExtensions.ToRussianString()` for weapon types, creature types, skill categories

## Key Files

- `CampaignManager.Web/Program.cs` — DI, auth, middleware, SignalR config
- `CampaignManager.Web/Utilities/DataBase/AppDbContext.cs` — Entity configuration, JSONB mappings
- `CampaignManager.Web/Utilities/DataBase/AppIdentityDbContext.cs` — Identity schema
- `CampaignManager.Web/Model/BaseDataBaseEntity.cs` — Base entity with Guid v7
- `CampaignManager.Web/Components/_Imports.razor` — Global using directives
- `CampaignManager.Web/Components/Features/` — All feature vertical slices
