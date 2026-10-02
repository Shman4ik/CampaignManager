using CampaignManager.Contracts.Scenarios;
using CampaignManager.Server.Platform;

namespace CampaignManager.Server.Scenarios;

/// <summary>Сценарии: библиотека, рабочее место, части по строке, состав. Знание модуля — <c>Scenarios/CLAUDE.md</c>.</summary>
public static class ScenariosModule
{
    private const string Tag = "Scenarios";

    public static WebApplicationBuilder AddScenariosModule(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ScenarioService>();
        builder.Services.AddScoped<ScenarioPartsService>();
        builder.Services.AddScoped<ScenarioCastService>();
        builder.Services.AddScoped<ScenarioExchangeService>();
        builder.Services.AddScoped<ScenarioPlayService>();
        builder.Services.AddScoped<RunService>();
        return builder;
    }

    /// <summary>Все эндпоинты — только вошедшим; кто что может, решают сервисы (<c>AccessPolicy</c>).</summary>
    public static IEndpointRouteBuilder MapScenariosApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("").RequireAuthorization().WithTags(Tag);

        group.MapGet(ScenariosRoutes.Scenarios, (ScenarioService s, CancellationToken ct) => s.ListAsync(ct)).WithName("ListScenarios");
        group.MapPost(ScenariosRoutes.Scenarios, async Task<IResult> (ScenarioInput input, ScenarioService s, CancellationToken ct) =>
        {
            var created = await s.CreateAsync(input, ct);
            return TypedResults.Created(ScenariosRoutes.Scenario(created.Id), created);
        }).WithName("CreateScenario");
        group.MapGet(ScenariosRoutes.ScenarioPattern, async Task<IResult> (Guid scenarioId, HttpContext http, ScenarioService s, CancellationToken ct) =>
        {
            var scenario = await s.GetAsync(scenarioId, ct);
            // Сценарий правят с двух устройств: браузер не должен показывать копию из кэша.
            http.Response.Headers.CacheControl = "private, no-store";
            http.Response.Headers.ETag = $"\"{scenario.Version}\"";
            return TypedResults.Ok(scenario);
        }).WithName("GetScenario");
        group.MapPut(ScenariosRoutes.ScenarioPattern, (Guid scenarioId, ScenarioInput input, HttpRequest request, ScenarioService s, CancellationToken ct) =>
            s.UpdateAsync(scenarioId, input, HttpIfMatch.Version(request), ct)).WithName("UpdateScenario");
        group.MapDelete(ScenariosRoutes.ScenarioPattern, async (Guid scenarioId, ScenarioService s, CancellationToken ct) =>
        {
            await s.DeleteAsync(scenarioId, ct);
            return TypedResults.NoContent();
        }).WithName("DeleteScenario");
        group.MapPut(ScenariosRoutes.TextPattern, (Guid scenarioId, ScenarioTextInput input, HttpRequest request, ScenarioService s, CancellationToken ct) =>
            s.SaveTextAsync(scenarioId, input, HttpIfMatch.Version(request), ct)).WithName("SaveScenarioText");
        group.MapPut(ScenariosRoutes.OrderPattern, async (Guid scenarioId, ReorderRequest input, ScenarioService s, CancellationToken ct) =>
        {
            await s.ReorderAsync(scenarioId, input, ct);
            return TypedResults.NoContent();
        }).WithName("ReorderScenarioPart");

        group.MapPut(ScenariosRoutes.LocationMusicPattern, (Guid scenarioId, Guid locationId, LocationMusicInput input, ScenarioPlayService p,
            CancellationToken ct) => p.SetLocationMusicAsync(scenarioId, locationId, input, ct)).WithName("SetScenarioLocationMusic");
        group.MapGet(ScenariosRoutes.HandoutScreenPattern, async Task<IResult> (Guid scenarioId, Guid handoutId, HttpContext http,
            ScenarioPlayService p, CancellationToken ct) =>
        {
            var handout = await p.GetHandoutScreenAsync(scenarioId, handoutId, ct);
            http.Response.Headers.CacheControl = "private, no-store";
            return TypedResults.Ok(handout);
        }).WithName("GetHandoutScreen");

        group.MapPost(ScenariosRoutes.LocationsPattern, (Guid scenarioId, LocationInput input, ScenarioPartsService p, CancellationToken ct) =>
            p.AddLocationAsync(scenarioId, input, ct)).WithName("AddScenarioLocation");
        group.MapPut(ScenariosRoutes.LocationPattern, (Guid scenarioId, Guid locationId, LocationInput input, ScenarioPartsService p, CancellationToken ct) =>
            p.UpdateLocationAsync(scenarioId, locationId, input, ct)).WithName("UpdateScenarioLocation");
        group.MapDelete(ScenariosRoutes.LocationPattern, async (Guid scenarioId, Guid locationId, ScenarioPartsService p, CancellationToken ct) =>
        {
            await p.DeleteLocationAsync(scenarioId, locationId, ct);
            return TypedResults.NoContent();
        }).WithName("DeleteScenarioLocation");

        group.MapPost(ScenariosRoutes.LocationChecksPattern, (Guid scenarioId, Guid locationId, CheckInput input, ScenarioPartsService p, CancellationToken ct) =>
            p.AddCheckAsync(scenarioId, locationId, input, ct)).WithName("AddScenarioCheck");
        group.MapPut(ScenariosRoutes.CheckPattern, (Guid scenarioId, Guid checkId, CheckInput input, ScenarioPartsService p, CancellationToken ct) =>
            p.UpdateCheckAsync(scenarioId, checkId, input, ct)).WithName("UpdateScenarioCheck");
        group.MapDelete(ScenariosRoutes.CheckPattern, async (Guid scenarioId, Guid checkId, ScenarioPartsService p, CancellationToken ct) =>
        {
            await p.DeleteCheckAsync(scenarioId, checkId, ct);
            return TypedResults.NoContent();
        }).WithName("DeleteScenarioCheck");

        group.MapPost(ScenariosRoutes.FactsPattern, (Guid scenarioId, KeyFactInput input, ScenarioPartsService p, CancellationToken ct) =>
            p.AddFactAsync(scenarioId, input, ct)).WithName("AddScenarioFact");
        group.MapPut(ScenariosRoutes.FactPattern, (Guid scenarioId, Guid factId, KeyFactInput input, ScenarioPartsService p, CancellationToken ct) =>
            p.UpdateFactAsync(scenarioId, factId, input, ct)).WithName("UpdateScenarioFact");
        group.MapDelete(ScenariosRoutes.FactPattern, async (Guid scenarioId, Guid factId, ScenarioPartsService p, CancellationToken ct) =>
        {
            await p.DeleteFactAsync(scenarioId, factId, ct);
            return TypedResults.NoContent();
        }).WithName("DeleteScenarioFact");

        group.MapPost(ScenariosRoutes.HandoutsPattern, (Guid scenarioId, HandoutInput input, ScenarioPartsService p, CancellationToken ct) =>
            p.AddHandoutAsync(scenarioId, input, ct)).WithName("AddScenarioHandout");
        group.MapPut(ScenariosRoutes.HandoutPattern, (Guid scenarioId, Guid handoutId, HandoutInput input, ScenarioPartsService p, CancellationToken ct) =>
            p.UpdateHandoutAsync(scenarioId, handoutId, input, ct)).WithName("UpdateScenarioHandout");
        group.MapDelete(ScenariosRoutes.HandoutPattern, async (Guid scenarioId, Guid handoutId, ScenarioPartsService p, CancellationToken ct) =>
        {
            await p.DeleteHandoutAsync(scenarioId, handoutId, ct);
            return TypedResults.NoContent();
        }).WithName("DeleteScenarioHandout");

        group.MapPost(ScenariosRoutes.CreaturesPattern, (Guid scenarioId, ScenarioCreatureInput input, ScenarioPartsService p, CancellationToken ct) =>
            p.AddCreatureAsync(scenarioId, input, ct)).WithName("AddScenarioCreature");
        group.MapPut(ScenariosRoutes.CreaturePattern, (Guid scenarioId, Guid creatureRowId, ScenarioCreatureInput input, ScenarioPartsService p,
            CancellationToken ct) => p.UpdateCreatureAsync(scenarioId, creatureRowId, input, ct)).WithName("UpdateScenarioCreature");
        group.MapDelete(ScenariosRoutes.CreaturePattern, async (Guid scenarioId, Guid creatureRowId, ScenarioPartsService p, CancellationToken ct) =>
        {
            await p.DeleteCreatureAsync(scenarioId, creatureRowId, ct);
            return TypedResults.NoContent();
        }).WithName("DeleteScenarioCreature");

        group.MapPost(ScenariosRoutes.ItemsPattern, (Guid scenarioId, ScenarioItemInput input, ScenarioPartsService p, CancellationToken ct) =>
            p.AddItemAsync(scenarioId, input, ct)).WithName("AddScenarioItem");
        group.MapPut(ScenariosRoutes.ItemPattern, (Guid scenarioId, Guid itemRowId, ScenarioItemInput input, ScenarioPartsService p, CancellationToken ct) =>
            p.UpdateItemAsync(scenarioId, itemRowId, input, ct)).WithName("UpdateScenarioItem");
        group.MapDelete(ScenariosRoutes.ItemPattern, async (Guid scenarioId, Guid itemRowId, ScenarioPartsService p, CancellationToken ct) =>
        {
            await p.DeleteItemAsync(scenarioId, itemRowId, ct);
            return TypedResults.NoContent();
        }).WithName("DeleteScenarioItem");

        group.MapPut(ScenariosRoutes.NpcPattern, async (Guid scenarioId, Guid characterId, NpcCastInput input, ScenarioCastService c, CancellationToken ct) =>
        {
            await c.CastNpcAsync(scenarioId, characterId, input, ct);
            return TypedResults.NoContent();
        }).WithName("CastScenarioNpc");
        group.MapDelete(ScenariosRoutes.NpcPattern, async (Guid scenarioId, Guid characterId, ScenarioCastService c, CancellationToken ct) =>
        {
            await c.RemoveNpcAsync(scenarioId, characterId, ct);
            return TypedResults.NoContent();
        }).WithName("RemoveScenarioNpc");
        group.MapPost(ScenariosRoutes.PregensPattern, (Guid scenarioId, AddPregenRequest input, ScenarioCastService c, CancellationToken ct) =>
            c.AddPregenAsync(scenarioId, input.PregenId, ct)).WithName("AddScenarioPregen");
        group.MapDelete(ScenariosRoutes.PregenPattern, async (Guid scenarioId, Guid characterId, ScenarioCastService c, CancellationToken ct) =>
        {
            await c.RemovePregenAsync(scenarioId, characterId, ct);
            return TypedResults.NoContent();
        }).WithName("RemoveScenarioPregen");

        ScenarioExchangeEndpoints.Map(group);
        RunEndpoints.Map(group);
        return app;
    }
}
