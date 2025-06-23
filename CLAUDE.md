# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CampaignManager is a tabletop RPG management system built with .NET 9 Blazor Server. It helps manage campaigns,
characters, scenarios, and game assets like creatures, items, weapons, spells, and skills. The application uses
PostgreSQL with Entity Framework Core and includes Google OAuth authentication.

## Development Commands

### Running the Application

```bash
# Run the web application (preferred method from copilot instructions)
dotnet run --project CampaignManager.Web

# Alternative: Run using AppHost
dotnet run --project CampaignManager.AppHost
```

### Database Operations

```bash
# Add new migration
dotnet ef migrations add MigrationName --project CampaignManager.Web --context AppDbContext

# Add identity migration
dotnet ef migrations add MigrationName --project CampaignManager.Web --context AppIdentityDbContext

# Update database
dotnet ef database update --project CampaignManager.Web --context AppDbContext
dotnet ef database update --project CampaignManager.Web --context AppIdentityDbContext
```

### Build and Development

```bash
# Build solution
dotnet build

# Restore packages
dotnet restore

# Clean solution
dotnet clean
```

## Architecture

### Feature-Based Vertical Slicing

The application uses a feature-based architecture where each domain is organized in
`/Components/Features/{FeatureName}/` with:

- **Components/** - Feature-specific reusable UI components
- **Model/** (or Models/) - Domain models and DTOs
- **Pages/** - Razor pages representing full views
- **Services/** - Business logic and data access services

Features include: Campaigns, Characters, Bestiary, Scenarios, Skills, Weapons, Spells, Items, Npс.

### Data Architecture

- **Database**: PostgreSQL with heavy JSONB usage for complex nested data
- **Base Entity**: All entities inherit from `BaseDataBaseEntity` with `Id`, `CreatedAt`, `LastUpdated`
- **Contexts**: Separate `AppDbContext` for application data and `AppIdentityDbContext` for identity
- **Factories**: Uses `IDbContextFactory<T>` pattern for thread-safe database access

### Key Entities and Relationships

- **Campaign** (1) ↔ (N) **CampaignPlayer** (1) ↔ (N) **CharacterStorageDto**
- **Scenario** can link to **Campaign** and contain NPCs via **CharacterStorageDto**
- Independent entities: **Creature**, **Item**, **Weapon**, **Spell**, **SkillModel**
- Complex data stored as JSONB (character stats, creature characteristics, scenario data)

### Service Patterns

All feature services follow consistent patterns:

- Constructor injection with primary constructor syntax
- `IDbContextFactory<AppDbContext>` for database access
- `IdentityService` for user context
- `ILogger<T>` for structured logging
- Async/await for all data operations
- Scoped lifetime registration in `Program.cs`

### Security and Authentication

- Google OAuth with cookie authentication
- Role-based authorization (Player, GameMaster, Administrator)
- `IdentityService` for centralized user identity management
- All sensitive operations require authentication

### UI Patterns

- Blazor Server with `InteractiveServer` render mode
- Tailwind CSS with custom design system (`design-system.css`)
- Shared components in `/Components/Shared/`
- Consistent parameter binding and event callback patterns
- SignalR with increased message size limits for large data transfers

## Configuration

### Database Configuration

- Uses `NpgsqlDataSourceBuilder` with dynamic JSON support
- Connection string: `DefaultConnection`
- Legacy timestamp behavior enabled for Npgsql

### External Services

- **Minio**: Object storage service for file management
- **QuestPDF**: PDF generation for character sheets
- **Markdig**: Markdown processing
- **Swagger**: API documentation at `/swagger`

### Authentication Setup

- Google OAuth requires `Authentication:Google:ClientId` and `Authentication:Google:ClientSecret` configuration
- Cookie settings configured for production (secure, SameSite=None)

## Development Notes

### Database Migrations

The project has extensive migration history. Key migrations include character management, weapons, spells, scenarios,
creatures, items, and skills. Always create migrations for both contexts when adding identity-related changes.

### JSONB Usage

Many entities use PostgreSQL JSONB columns for complex data structures. This includes character statistics, creature
characteristics, and scenario relationships. Be mindful of JSONB serialization when working with these fields.

### Memory and Performance

- SignalR configured with increased message size limits (2MB)
- Blazor Server optimized for large updates (20 buffered render batches)
- Memory caching implemented for frequently accessed data (e.g., Skills)
- Output caching enabled

### File Organization

- Razor components use `@rendermode InteractiveServer`
- All imports defined in `_Imports.razor`
- Static files in `wwwroot/` with custom CSS and JavaScript
- Design system documentation in `wwwroot/design-system-guide.md`