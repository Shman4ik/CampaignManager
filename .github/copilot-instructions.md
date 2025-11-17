# GitHub Copilot Instructions for CampaignManager

This file provides GitHub Copilot with context and coding standards for the CampaignManager project, following .NET 10 and modern Blazor best practices.

## Project Overview

CampaignManager is a tabletop RPG management system built with:
- **.NET 10** - Latest .NET framework with C# 12 features
- **Blazor Server** - Interactive server-side rendering with SignalR
- **PostgreSQL** - Database with extensive JSONB usage for complex data
- **Entity Framework Core 10** - ORM with factory pattern for thread safety
- **Google OAuth** - Authentication and authorization
- **Tailwind CSS** - Utility-first CSS with custom design system

## Quick Commands

### Running the Application

```bash
# Preferred method
dotnet run --project CampaignManager.Web

# Alternative with AppHost (.NET Aspire)
dotnet run --project CampaignManager.AppHost
```

### Build and Restore

```bash
dotnet restore
dotnet build
dotnet clean
```

### Database Migrations

```bash
# Application data context
dotnet ef migrations add <Name> --project CampaignManager.Web --context AppDbContext
dotnet ef database update --project CampaignManager.Web --context AppDbContext

# Identity context
dotnet ef migrations add <Name> --project CampaignManager.Web --context AppIdentityDbContext
dotnet ef database update --project CampaignManager.Web --context AppIdentityDbContext
```

## Architecture and Code Organization

### Feature-Based Vertical Slicing

Code is organized by business domain features, not technical layers. Each feature lives in `/Components/Features/{FeatureName}/`:

```
Features/
├── {FeatureName}/
│   ├── Components/      # Feature-specific reusable UI components
│   ├── Model/          # Domain models, DTOs, enums (or Models/)
│   ├── Pages/          # Full Razor page views
│   └── Services/       # Business logic and data access
```

**Current Features**: Campaigns, Characters, Bestiary, Scenarios, Skills, Weapons, Spells, Items, Npс, Combat

**Benefits**: Improved maintainability, easier feature location, better team scalability, clear boundaries.

### Key Files and Locations

- `CampaignManager.Web/Program.cs` — DI registrations, authentication, service wiring, SignalR configuration
- `CampaignManager.AppHost/Program.cs` — .NET Aspire host entry point
- `CampaignManager.Web/Utilities/DataBase/` — DbContext configurations and base entities
- `CampaignManager.Web/Components/Features/` — Feature-based vertical slices
- `CampaignManager.Web/Components/Shared/` — Reusable shared components
- `CampaignManager.Web/Migrations/` — EF Core migration history
- `CampaignManager.Web/wwwroot/design-system.css` — Custom design system
- `CampaignManager.Web/wwwroot/design-system-guide.md` — Design system documentation

## .NET 10 and C# 12 Coding Standards

### Use Primary Constructors (Required Pattern)

**✅ Correct - Use primary constructors for dependency injection:**
```csharp
public sealed class SkillService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IMemoryCache cache,
    ILogger<SkillService> logger)
{
    // Parameters automatically available as fields
    public async Task<List<SkillModel>> GetAllAsync()
    {
        // Use injected dependencies directly
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        logger.LogInformation("Loading skills");
        // ...
    }
}
```

**❌ Incorrect - Don't use traditional constructor syntax:**
```csharp
public class SkillService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public SkillService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }
}
```

### C# 12 Features to Use

**Collection Expressions:**
```csharp
return []; // Instead of new List<T>()
List<int> numbers = [1, 2, 3]; // Instead of new List<int> { 1, 2, 3 }
```

**Pattern Matching:**
```csharp
if (skill is null) return false;
if (skills is not null) return FilterAndPage(skills);
```

**Nullable Reference Types (Always Enabled):**
```csharp
public Task<SkillModel?> GetByIdAsync(int id); // ? indicates nullable
string name = ""; // Non-nullable must be initialized
```

### Service Layer Patterns (Required)

**All services MUST follow these conventions:**

1. **Sealed classes** - Use `sealed` for performance (prevents inheritance overhead)
2. **Primary constructor syntax** - For dependency injection
3. **IDbContextFactory<T>** - For thread-safe database access
4. **ILogger<T>** - For structured logging
5. **Async/await** - For all I/O operations
6. **Scoped lifetime** - Registration in Program.cs

**Standard Service Template:**
```csharp
namespace CampaignManager.Web.Components.Features.{FeatureName}.Services;

/// <summary>
/// Service for managing {feature} operations
/// </summary>
public sealed class {FeatureName}Service(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IdentityService identityService,
    ILogger<{FeatureName}Service> logger)
{
    /// <summary>
    /// Gets all {entities} for the current user
    /// </summary>
    public async Task<List<{Entity}>> GetAllAsync()
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var userId = identityService.GetCurrentUserId();

            return await dbContext.{Entities}
                .Where(e => e.UserId == userId)
                .OrderBy(e => e.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving {entities}");
            return [];
        }
    }
}
```

**Service Registration:**
```csharp
// In Program.cs
builder.Services.AddScoped<{FeatureName}Service>();
```

## Entity Framework Core Best Practices

### DbContext Usage (CRITICAL)

**✅ ALWAYS use factory pattern with await using:**
```csharp
await using var dbContext = await dbContextFactory.CreateDbContextAsync();
```

**❌ NEVER share DbContext across threads or store as field:**
```csharp
// DbContext is NOT thread-safe!
private readonly AppDbContext _dbContext; // ❌ WRONG
```

### Base Entity Pattern

All entities inherit from `BaseDataBaseEntity`:
```csharp
public class YourEntity : BaseDataBaseEntity
{
    // Id, CreatedAt, LastUpdated are inherited automatically
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
```

### JSONB Columns for Complex Data

Use PostgreSQL JSONB for nested/complex structures:
```csharp
// In entity
public ComplexData Data { get; set; } = new();

// In DbContext OnModelCreating or IEntityTypeConfiguration
builder.Property(e => e.Data)
    .HasColumnType("jsonb");
```

**Important:** Be careful when changing model shapes - JSONB serialization is sensitive to schema changes.

### Entity Configuration Pattern

Extract configurations into separate classes:
```csharp
public class SkillConfiguration : IEntityTypeConfiguration<SkillModel>
{
    public void Configure(EntityTypeBuilder<SkillModel> builder)
    {
        builder.HasIndex(s => s.Name);
        builder.Property(s => s.Name)
            .HasMaxLength(200)
            .IsRequired();
    }
}

// In AppDbContext:
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfiguration(new SkillConfiguration());
}
```

### Performance Optimization

1. **Index frequently queried columns:**
```csharp
builder.HasIndex(e => e.UserId);
builder.HasIndex(e => e.CreatedAt);
```

2. **Specify precise string lengths** (avoid nvarchar(max)):
```csharp
builder.Property(e => e.Name).HasMaxLength(200);
```

3. **Use `.AsNoTracking()` for read-only queries:**
```csharp
return await dbContext.Skills
    .AsNoTracking()
    .ToListAsync();
```

4. **Implement caching for frequently accessed, rarely changing data**

## Blazor Server Best Practices

### Component Structure

**Standard Blazor component template:**
```razor
@page "/route"
@rendermode InteractiveServer
@inject ServiceName Service
@inject ILogger<ComponentName> Logger

<div class="container mx-auto px-4">
    @* Component markup *@
</div>

@code {
    [Parameter]
    public int Id { get; set; }

    [CascadingParameter]
    private Task<AuthenticationState>? AuthState { get; set; }

    private List<Model> items = [];
    private bool isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            items = await Service.GetAllAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading data");
        }
        finally
        {
            isLoading = false;
        }
    }
}
```

### Render Modes

**Use Interactive Server** for components requiring interactivity:
```razor
@rendermode InteractiveServer
```

This is configured globally in Program.cs with optimized SignalR settings.

### Event Callbacks

```razor
@code {
    [Parameter]
    public EventCallback OnSave { get; set; }

    [Parameter]
    public EventCallback<int> OnItemSelected { get; set; }

    private async Task HandleClick()
    {
        await OnItemSelected.InvokeAsync(selectedId);
    }
}
```

### Form Validation (.NET 10 Features)

Use improved nested object validation:
```csharp
// Mark complex types with [ValidatableType]
public class Character
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [ValidatableType] // .NET 10 feature - enables nested validation
    public PersonalInfo PersonalInfo { get; set; } = new();
}
```

### Performance Best Practices

1. **Use `@key` directive** for collections:
```razor
@foreach (var item in items)
{
    <ItemComponent @key="item.Id" Item="@item" />
}
```

2. **Virtualization for long lists:**
```razor
<Virtualize Items="@items" Context="item">
    <ItemTemplate>
        <div>@item.Name</div>
    </ItemTemplate>
</Virtualize>
```

3. **Avoid unnecessary re-renders** with `ShouldRender()`:
```csharp
protected override bool ShouldRender()
{
    return hasChanges; // Only re-render when needed
}
```

4. **Use `StateHasChanged()` after state updates:**
```csharp
private async Task UpdateData()
{
    items = await Service.GetAllAsync();
    StateHasChanged(); // Trigger re-render
}
```

### Persistent State (.NET 10)

For circuit state persistence when evicted from memory:
```csharp
[PersistentState]
private string? FormData { get; set; }
```

This integrates with .NET 10's circuit eviction and resumption features.

## Security and Authorization

### Authentication Pattern

```razor
<AuthorizeView Roles="GameMaster,Administrator">
    <Authorized>
        @* Content for authorized users *@
        <p>Welcome, @context.User.Identity?.Name</p>
    </Authorized>
    <NotAuthorized>
        @* Content for unauthorized users *@
        <p>Please log in to access this feature.</p>
    </NotAuthorized>
</AuthorizeView>
```

### Service-Level Authorization

```csharp
public sealed class SecureService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IdentityService identityService,
    ILogger<SecureService> logger)
{
    public async Task<Result> PerformActionAsync()
    {
        var userId = identityService.GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogWarning("Unauthorized access attempt");
            return Result.Unauthorized();
        }

        // Perform authorized action
    }
}
```

### Roles

- `Player` - Standard user
- `GameMaster` - Campaign manager
- `Administrator` - System administrator

## UI Patterns and Styling

### Tailwind CSS

Use utility classes following the design system:
```html
<div class="container mx-auto px-4">
    <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Title</h1>
    <button class="btn btn-primary">Action</button>
</div>
```

**Design System:** Custom classes defined in `wwwroot/design-system.css`, documented in `wwwroot/design-system-guide.md`.

### Component Naming Conventions

- **Pages:** `{Entity}Page.razor`, `{Entity}EditPage.razor`
- **Components:** `{Entity}Card.razor`, `{Entity}Form.razor`, `{Entity}List.razor`
- **Modals:** `Add{Entity}Modal.razor`, `Edit{Entity}Modal.razor`

### Shared Components

Reusable components in `/Components/Shared/`:
- Modal dialogs
- Loading indicators
- Pagination controls
- Form inputs
- Navigation components

## Logging and Error Handling

### Structured Logging (Required)

**✅ Use structured logging with context:**
```csharp
logger.LogInformation("Loading skill with ID {SkillId}", skillId);
logger.LogWarning("Skill {SkillName} already exists", skillName);
logger.LogError(ex, "Error creating skill {SkillName}", skillName);
```

**❌ Don't use string interpolation:**
```csharp
logger.LogError($"Error: {message}"); // ❌ WRONG
```

### Error Handling Pattern

```csharp
try
{
    // Operation
    return result;
}
catch (Exception ex)
{
    logger.LogError(ex, "Error description with {Context}", context);
    return []; // or appropriate default value
}
```

## Common Patterns

### CRUD Service Pattern

```csharp
public sealed class EntityService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IdentityService identityService,
    ILogger<EntityService> logger)
{
    public async Task<List<Entity>> GetAllAsync() { /* ... */ }
    public async Task<Entity?> GetByIdAsync(int id) { /* ... */ }
    public async Task<Entity?> CreateAsync(Entity entity) { /* ... */ }
    public async Task<bool> UpdateAsync(Entity entity) { /* ... */ }
    public async Task<bool> DeleteAsync(int id) { /* ... */ }
}
```

### Pagination Pattern

```csharp
public async Task<List<T>> GetPagedAsync(int page = 1, int pageSize = 10)
{
    await using var dbContext = await dbContextFactory.CreateDbContextAsync();
    return await dbContext.Set<T>()
        .OrderBy(e => e.Name)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();
}

public async Task<int> GetCountAsync()
{
    await using var dbContext = await dbContextFactory.CreateDbContextAsync();
    return await dbContext.Set<T>().CountAsync();
}
```

### Memory Caching Pattern

```csharp
public sealed class CachedService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IMemoryCache cache,
    ILogger<CachedService> logger)
{
    private const string CacheKey = "EntityCache";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);

    public async Task<List<Entity>> GetAllAsync()
    {
        // Try to get from cache
        if (cache.TryGetValue(CacheKey, out List<Entity>? entities) && entities is not null)
            return entities;

        // Load from database
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        entities = await dbContext.Entities.ToListAsync();

        // Store in cache
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(CacheExpiration);
        cache.Set(CacheKey, entities, cacheOptions);

        return entities;
    }

    // Invalidate cache after modifications
    public async Task<bool> CreateAsync(Entity entity)
    {
        var result = await CreateInternalAsync(entity);
        if (result) cache.Remove(CacheKey);
        return result;
    }
}
```

## External Services and Configuration

### Required Configuration

- `ConnectionStrings:DefaultConnection` - PostgreSQL connection string
- `Authentication:Google:ClientId` - Google OAuth client ID
- `Authentication:Google:ClientSecret` - Google OAuth client secret

### External Integrations

- **PostgreSQL / Npgsql** - Database with JSONB support, legacy timestamp behavior enabled
- **Minio** - Object storage service for file management
- **QuestPDF** - PDF generation for character sheets and exports
- **Markdig** - Markdown processing for rich text content
- **Swagger** - API documentation available at `/swagger`
- **.NET Aspire** - Cloud-native orchestration with AppHost project

## Important Rules and Reminders

1. **Always use `sealed`** for service classes unless inheritance is explicitly required
2. **Use primary constructors** for all new services and classes (.NET 10 / C# 12)
3. **DbContext is NOT thread-safe** - always use `IDbContextFactory<T>` with `await using`
4. **Await all async calls** before continuing to use the context
5. **Use structured logging** with context parameters, NOT string interpolation
6. **Validate input** at both client and server levels
7. **Handle exceptions gracefully** and log with context
8. **Nullable reference types** are enabled - use `?` for nullable types
9. **Follow naming conventions** for components, services, and files
10. **Single responsibility** - keep components and services focused
11. **Two DbContexts** - `AppDbContext` (app data) and `AppIdentityDbContext` (identity)
12. **JSONB-heavy storage** - be careful when changing model shapes
13. **Feature-based organization** - look in `Components/Features/{FeatureName}`
14. **SignalR is configured** - 2MB message limit, optimized for large updates

## Debugging Tips

1. **Primary entry point:** Use `CampaignManager.Web` when testing UI changes
2. **Logs:** All services use injected `ILogger<T>` for structured logging
3. **Database issues:** Check migrations in `Migrations/` folders for schema history
4. **Authentication issues:** Check `Program.cs` for auth configuration and Google OAuth setup
5. **Performance issues:** Check SignalR configuration and circuit limits in `Program.cs`
6. **JSONB issues:** Inspect PostgreSQL columns directly for serialization problems

---

**This is a .NET 10 Blazor Server application using modern C# 12 features and best practices. Always prefer the latest patterns and avoid outdated approaches.**

When in doubt, refer to `Program.cs` and the feature folder for the area you're modifying - they demonstrate the canonical patterns used throughout the application.
