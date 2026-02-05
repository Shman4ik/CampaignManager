using CampaignManager.Web.Components.Features.Characters.Services;
using CampaignManager.Web.Components.Features.Skills.Model;
using CampaignManager.Web.Components.Features.Skills.Services;
using CampaignManager.Web.Utilities.DataBase;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CampaignManager.Web.Utilities.Api;

/// <summary>
/// API endpoints for character migration operations
/// </summary>
public static class CharacterMigrationApi
{
    /// <summary>
    /// Maps character migration API endpoints to the application's routing
    /// </summary>
    /// <param name="routes">The endpoint route builder</param>
    public static void MapCharacterMigrationEndpoints(this IEndpointRouteBuilder routes)
    {
        var migrationGroup = routes.MapGroup("/api/migration")
            .RequireAuthorization()
            .WithTags("Character Migration");

        migrationGroup.MapPost("/skills", MigrateCharacterSkillsAsync)
            .WithName("MigrateCharacterSkills")
            .WithSummary("Migrate character skills to link with SkillModel wiki entries")
            .WithDescription("One-time migration that links character skills to SkillModel entities by matching skill names. Requires GameMaster or Administrator role.");

        migrationGroup.MapPost("/skills/import-json", ImportSkillsFromJsonAsync)
            .WithName("ImportSkillsFromJson")
            .WithSummary("Import skills from JSON file and rebuild database")
            .WithDescription("Deletes all existing skills and imports from skills-coc7-extracted.json. Requires GameMaster or Administrator role.");
    }

    /// <summary>
    /// Migrates character skills to link them with SkillModel entities
    /// </summary>
    /// <param name="characterService">Character service for migration operations</param>
    /// <param name="logger">Logger for operation tracking</param>
    /// <returns>
    /// <list type="bullet">
    /// <item><description>200 OK - Migration completed with results</description></item>
    /// <item><description>401 Unauthorized - User is not authenticated</description></item>
    /// <item><description>403 Forbidden - User is not authorized (must be GameMaster or Admin)</description></item>
    /// <item><description>500 Internal Server Error - Migration failed</description></item>
    /// </list>
    /// </returns>
    private static async Task<Results<Ok<SkillMigrationResult>, UnauthorizedHttpResult, StatusCodeHttpResult>> MigrateCharacterSkillsAsync(
        CharacterService characterService,
        ILogger<Program> logger)
    {
        try
        {
            logger.LogInformation("Starting character skill migration");

            var result = await characterService.MigrateCharacterSkillsToSkillModelAsync();

            if (result.Success)
            {
                logger.LogInformation($"Skill migration completed successfully: {result.SkillsLinked} skills linked, {result.CharactersUpdated} characters updated");
                return TypedResults.Ok(result);
            }
            else
            {
                logger.LogError($"Skill migration failed: {result.ErrorMessage}");
                return TypedResults.StatusCode(500);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Unauthorized migration attempt");
            return TypedResults.Unauthorized();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during skill migration");
            return TypedResults.StatusCode(500);
        }
    }

    /// <summary>
    /// Imports skills from JSON file and rebuilds the Skills table
    /// </summary>
    /// <param name="dbContextFactory">Database context factory</param>
    /// <param name="webHostEnvironment">Web host environment for file path resolution</param>
    /// <param name="logger">Logger for operation tracking</param>
    /// <returns>
    /// <list type="bullet">
    /// <item><description>200 OK - Import completed successfully</description></item>
    /// <item><description>401 Unauthorized - User is not authenticated</description></item>
    /// <item><description>500 Internal Server Error - Import failed</description></item>
    /// </list>
    /// </returns>
    private static async Task<Results<Ok<SkillImportResult>, UnauthorizedHttpResult, StatusCodeHttpResult>> ImportSkillsFromJsonAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<Program> logger,
        IWebHostEnvironment webHostEnvironment)
    {
        try
        {
            logger.LogInformation("Starting skill import from JSON");

            var jsonFilePath = Path.Combine(webHostEnvironment.ContentRootPath, "Data", "skills-coc7-extracted.json");

            if (!File.Exists(jsonFilePath))
            {
                logger.LogError("JSON file not found at {FilePath}", jsonFilePath);
                return TypedResults.StatusCode(404);
            }

            var jsonContent = await File.ReadAllTextAsync(jsonFilePath);
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
            var skills = JsonSerializer.Deserialize<List<SkillModel>>(jsonContent, jsonOptions);

            if (skills == null || skills.Count == 0)
            {
                logger.LogError("Failed to deserialize skills from JSON or file is empty");
                return TypedResults.StatusCode(500);
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // Delete all existing skills using raw SQL for better control
            await dbContext.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS games.\"Skills\" CASCADE");

            // Recreate table with Guid ID
            await dbContext.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE games.""Skills"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""Name"" character varying(100) NOT NULL,
                    ""BaseValue"" integer NOT NULL,
                    ""Description"" character varying(1000) NOT NULL,
                    ""Category"" text NOT NULL,
                    ""IsUncommon"" boolean NOT NULL,
                    ""UsageExamples"" jsonb NOT NULL,
                    ""FailureConsequences"" jsonb NOT NULL,
                    ""TimeRequired"" text NOT NULL,
                    ""CanRetry"" boolean NOT NULL,
                    ""OpposingSkills"" jsonb NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""LastUpdated"" timestamp with time zone NOT NULL
                );
                CREATE UNIQUE INDEX ""IX_Skills_Name"" ON games.""Skills"" (""Name"");
            ");

            // Import skills with new Guid IDs
            var now = DateTimeOffset.UtcNow;
            foreach (var skill in skills)
            {
                skill.Id = Guid.CreateVersion7();
                skill.CreatedAt = now;
                skill.LastUpdated = now;
                await dbContext.Skills.AddAsync(skill);
            }

            await dbContext.SaveChangesAsync();

            logger.LogInformation("Successfully imported {SkillCount} skills from JSON", skills.Count);

            return TypedResults.Ok(new SkillImportResult
            {
                Success = true,
                SkillsImported = skills.Count,
                Message = $"Successfully imported {skills.Count} skills"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during skill import from JSON");
            return TypedResults.StatusCode(500);
        }
    }
}

/// <summary>
/// Result of skill import operation
/// </summary>
public class SkillImportResult
{
    public bool Success { get; set; }
    public int SkillsImported { get; set; }
    public string Message { get; set; } = string.Empty;
}
