using CampaignManager.Web.Components.Features.Bestiary.Services;

namespace CampaignManager.Web.Utilities.Api;

public static class CreatureMigrationApi
{
    public static void MapCreatureMigrationEndpoints(this IEndpointRouteBuilder routes)
    {
        var migrationGroup = routes.MapGroup("/api/migration")
            .RequireAuthorization("RequireAdministratorRole")
            .WithTags("Creature Migration");

        migrationGroup.MapPost("/creature-attacks", async (CreatureDataMigrationService migrationService) =>
            {
                var count = await migrationService.MigrateAsync();
                return Results.Ok(new { migratedCount = count });
            })
            .WithName("MigrateCreatureAttacks")
            .WithSummary("Migrate creature CombatDescriptions to structured Attacks");
    }
}
