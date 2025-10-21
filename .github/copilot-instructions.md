```markdown
# Copilot / AI agent quick instructions for CampaignManager

Use these concise, actionable notes to make safe and productive code changes.

- Run the web app (preferred):

  dotnet run --project CampaignManager.Web

  Alternative (AppHost):

  dotnet run --project CampaignManager.AppHost

- Build / restore:

  dotnet restore
  dotnet build

- Database / EF Core (always target the correct DbContext):

  # App data
  dotnet ef migrations add <Name> --project CampaignManager.Web --context AppDbContext
  dotnet ef database update --project CampaignManager.Web --context AppDbContext

  # Identity data
  dotnet ef migrations add <Name> --project CampaignManager.Web --context AppIdentityDbContext
  dotnet ef database update --project CampaignManager.Web --context AppIdentityDbContext

- Key places to inspect when changing behavior

  - `CampaignManager.Web/Program.cs` — DI registrations, authentication, and service wiring.
  - `CampaignManager.AppHost/Program.cs` — alternative host entry used in some deployments.
  - `CampaignManager.Web/Model/BaseDataBaseEntity.cs` — base entity (Id, CreatedAt, LastUpdated).
  - `CampaignManager.Web/Model/ApplicationUser.cs` — identity extension.
  - `CampaignManager.Web/Components/Features/` — feature-based vertical-slice layout (UI + services).
  - `CampaignManager.Web/Migrations/` — migration history for AppDb and AppIdentityDb.
  - `CampaignManager.Web/Utilities/` — shared helpers, DB access, external integrations.
  - `CampaignManager.Web/wwwroot/design-system-guide.md` — design system and Tailwind usage.

- Project-specific conventions and patterns

  - Feature-based organization: look under `Components/Features/{FeatureName}` for pages, components and services.
  - All services use constructor injection, `IDbContextFactory<T>` for EF Core access, async/await for DB calls.
  - JSONB-heavy storage: many domain objects serialize into JSONB columns — be careful when changing model shapes.
  - Two DbContexts: `AppDbContext` (application data) and `AppIdentityDbContext` (identity). When adding migrations or changing models, update the correct context.
  - Identity and auth: Google OAuth + cookie auth configured in `Program.cs`. Secrets live in configuration (appsettings or environment variables).

- External integrations to be aware of

  - PostgreSQL / Npgsql (JSONB usage, legacy timestamp behavior possibly enabled)
  - Minio (object storage)
  - QuestPDF (PDF generation)
  - Markdig (markdown processing)
  - Swagger at `/swagger`

- Quick debugging tips

  - Use the `CampaignManager.Web` project as the primary entry point when testing UI changes.
  - Inspect logs via injected `ILogger<T>`; many services rely on structured logging.
  - For data issues, use the EF migrations and inspect `Migrations/` folders for schema history.

When in doubt, open `CampaignManager.Web/Program.cs` and the feature folder for the area you’re modifying — they capture the canonical DI, auth, and service patterns used across the app.

Please tell me if any area is unclear or you want this shortened/expanded for a particular agent role (testing, refactoring, or feature work).
```
Use dotnet run --project CampaignManager.Web when you want to run the web application.