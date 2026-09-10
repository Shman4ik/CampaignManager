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

История миграций `AppDb` схлопнута в одну `20260910132807_InitialCreate` — сорок шесть
прежних миграций занимали 34 664 строки, две трети всего C# в проекте. Схема при этом
не изменилась: снапшот модели после схлопывания совпал с прежним побайтово.

- **Базу, накатанную до схлопывания, нужно перевести на новый журнал один раз** скриптом
  `docs/squash-migrations.sql` — он переписывает `__EFMigrationsHistory`, саму схему не трогает.
  Без этого `database update` решит, что схемы нет, и попробует создать её заново.
- Пустой базе скрипт не нужен: обычный `database update` создаст схему из `InitialCreate`.
- Миграции нигде не применяются автоматически — ни в `Program.cs`, ни в Dockerfile, ни в CI,
  так что момент накатывания выбирается вручную.
- Прежние миграции содержали только `UPDATE` существующих строк (backfill'ы оружия и
  бестиария), без `InsertData`, поэтому на новой базе схлопывание ничего не теряет.

**Tailwind CSS** is built automatically by the `Tailwind` MSBuild target in the csproj:
- Debug: `npx tailwindcss@3 -i ./Styles/tailwind.css -o ./wwwroot/styles.css`
- Release: same with `--minify`

`wwwroot/styles.css` is a build artifact and is **not** tracked in git — it is regenerated on
every build, so the deployed CSS always matches the current markup. Two things keep that working,
don't undo either:
- The target is hooked `BeforeTargets="ResolveProjectStaticWebAssets"`, and it adds the file to
  `@(Content)` itself. MSBuild expands the `wwwroot/**` glob at evaluation time, so a file created
  during the build is invisible to static web assets — without the explicit `Content Include` the
  build and the deploy both succeed and the site serves 404 for `/styles.css`.
- Building therefore requires `npx` (and network access on the first run). The Dockerfile copies
  Node into the SDK stage for exactly this reason.

**No test projects exist** in this solution.

## Работа в контейнере Claude Code on the web

В удалённом контейнере .NET SDK по умолчанию нет, а `dot.net` / `builds.dotnet.microsoft.com`
закрыты egress-политикой (`curl` получает 403 от прокси) — скрипт `dotnet-install.sh` там не качается.
Ставить надо из репозитория Ubuntu, где .NET 10 уже есть:

```bash
apt-get update                                  # без этого dotnet-sdk-10.0 не виден
DEBIAN_FRONTEND=noninteractive apt-get install -y dotnet-sdk-10.0
dotnet --version                                # 10.0.111 на noble-updates
```

`apt-get update` ругается на недоступные PPA (deadsnakes, ondrej) — это не мешает,
нужные индексы (`archive.ubuntu.com`, `packages.microsoft.com`) забираются.
`nuget.org` доступен, поэтому `dotnet restore` и `dotnet tool install` работают.

Дальше всё как обычно, с двумя оговорками:

```bash
dotnet build                                    # Tailwind собирается тем же таргетом, npx доступен

# EF: design-time поднимает Program.cs целиком, поэтому без строки подключения
# падает с «Value cannot be null. (Parameter 'Host')» — достаточно любой валидной
dotnet tool install --global dotnet-ef && export PATH="$PATH:/root/.dotnet/tools"
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=campaignmanager;Username=postgres;Password=postgres"
dotnet ef migrations has-pending-model-changes --project CampaignManager.Web --context AppDbContext
```

PostgreSQL 16 в контейнере уже установлен, только не запущен — на нём можно прогнать миграции
на живой базе и проверить перенос данных:

```bash
service postgresql start
su postgres -c "psql -c \"ALTER USER postgres PASSWORD 'postgres';\""
su postgres -c "createdb campaignmanager"
dotnet ef database update --project CampaignManager.Web --context AppDbContext
dotnet ef database update --project CampaignManager.Web --context AppIdentityDbContext
```

Запуск приложения: без Google OAuth **каждый** запрос отдаёт 500
(`ArgumentException: The value cannot be an empty string. (Parameter 'ClientId')` из
middleware аутентификации, ещё до роутинга) — это не поломка кода, а пустая конфигурация.
Хватает пустышек:

```bash
export Authentication__Google__ClientId=dummy-client-id
export Authentication__Google__ClientSecret=dummy-client-secret
dotnet CampaignManager.Web/bin/Debug/net10.0/CampaignManager.Web.dll --urls http://127.0.0.1:5199
```

`dotnet run` берёт URL из `launchSettings.json` (https://localhost:8080) и игнорирует
`ASPNETCORE_URLS`, поэтому для смоук-теста удобнее запускать собранную dll с `--urls`.
Страницы под `[Authorize]` без залогиненного пользователя отдают 302 на логин — это
ожидаемо; для проверки рендера годится `/`.

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
- Shared components in `Components/Shared/`: Badge, Button, Modal, ConfirmationModal, NotificationAlert, Pagination, FilterPanel, LoadingIndicator, EmptyState, etc.

#### Page shell — the same on every page

Every routable page is `<PageHeader Title="…">` (with page-level actions in its `Actions` slot)
followed by one `<div class="cm-page">`. `Components/Pages/Home.razor` is the reference; the info
pages, the character sheet and the scenario detail page all use it too. Inside, group with
`cm-section` + `cm-section-title` and `cm-card` + `cm-card-header`/`-body`/`-footer`.

- **Never** wrap page content in `max-w-*` + `mx-auto`. `page-with-sidebar` is a column flex
  container, so `mx-auto` on a flex item disables stretch and collapses the page to its content
  width — that is why sparse pages used to render as a narrow centred column.
- Buttons carry meaning by colour: `primary` = the one main action, `secondary` = neutral,
  `outline-primary`/`outline-error` = row edit/delete, `error` = destructive confirmation in a
  dialog, `success` = approving someone's request. `cm-btn-info` is not for buttons.
- Header actions are always `cm-btn-sm` — the topbar is 56px tall.
- Status colours (`--color-success-*`, `--color-warning-*`, `--color-error-*`) are defined in
  **both** `tailwind.config.js` and `:root` in `design-system.css`; keep them in sync, otherwise
  `cm-btn-error` and `<Button Variant="error">` render different reds.
- Full details and the class inventory: `wwwroot/design-system-guide.md`.

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

## Circuit State Persistence

Blazor Server keeps page state in a server-side circuit, so a dropped connection, a backgrounded
iPad tab, or a deployment would otherwise wipe whatever the Keeper had on screen. The app opts into
the .NET 10 circuit persistence stack:

- `[PersistentState]` on a **public** property is what gets saved and restored. A service opts in by
  being registered with `RegisterPersistentService<T>(RenderMode.InteractiveServer)` in `Program.cs`
  (`CombatService` and `ChaseService` today). The getter runs when the circuit is paused, the setter
  when it resumes — expose **one snapshot property** per service rather than marking every field.
- The persisted value must be JSON-serializable: plain POCOs, no cycles, no lazy EF navigations.
- Retention is configured on `CircuitOptions` (`PersistedCircuitInMemoryMaxRetained`,
  `PersistedCircuitInMemoryRetentionPeriod`). That state lives in the server's memory and does **not**
  survive a process restart.
- Surviving a restart relies on pausing circuits *before* shutdown: `Utilities/Circuits` asks every
  connected tab to call `Blazor.pauseCircuit()` from `IHostedService.StopAsync`, which moves the state
  into the browser. Data Protection keys are stored in PostgreSQL, so the new instance can unprotect
  what the browser sends back. .NET 11 replaces this with `Circuit.RequestCircuitPauseAsync`.
- Client side: `wwwroot/js/circuit-persistence.js` pauses on tab hide and retries resume with backoff.
  The (Russian) reconnect dialog is `#components-reconnect-modal` in `App.razor` plus
  `wwwroot/css/reconnect.css` — the `components-reconnect-*` class names come from the framework,
  don't rename them.
- Adding a field to a persisted service's state? Add it to that service's snapshot type as well,
  otherwise it silently disappears on resume.

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
