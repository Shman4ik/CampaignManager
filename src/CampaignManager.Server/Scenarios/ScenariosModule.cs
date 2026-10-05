using CampaignManager.Contracts.Identity;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Server.Identity;
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
        // Всё, кроме прохождений, открыто токену агента со scope сценариев: прохождения и брони трогают кампании.
        var scenarios = group.MapGroup("").AllowMachine(MachineScopes.Scenarios);

        scenarios.MapGet(ScenariosRoutes.Scenarios, (ScenarioService s, CancellationToken ct) => s.ListAsync(ct)).WithName("ListScenarios");
        scenarios.MapPost(ScenariosRoutes.Scenarios, async Task<IResult> (ScenarioInput input, ScenarioService s, CancellationToken ct) =>
        {
            var created = await s.CreateAsync(input, ct);
            return TypedResults.Created(ScenariosRoutes.Scenario(created.Id), created);
        }).WithName("CreateScenario");
        scenarios.MapGet(ScenariosRoutes.ScenarioPattern, async Task<IResult> (Guid scenarioId, HttpContext http, ScenarioService s, CancellationToken ct) =>
        {
            var scenario = await s.GetAsync(scenarioId, ct);
            // Сценарий правят с двух устройств: браузер не должен показывать копию из кэша.
            http.Response.Headers.CacheControl = "private, no-store";
            http.Response.Headers.ETag = $"\"{scenario.Version}\"";
            return TypedResults.Ok(scenario);
        }).WithName("GetScenario");
        scenarios.MapPut(ScenariosRoutes.ScenarioPattern, (Guid scenarioId, ScenarioInput input, HttpRequest request, ScenarioService s, CancellationToken ct) =>
            s.UpdateAsync(scenarioId, input, HttpIfMatch.Version(request), ct)).WithName("UpdateScenario");
        scenarios.MapDelete(ScenariosRoutes.ScenarioPattern, async (Guid scenarioId, ScenarioService s, CancellationToken ct) =>
        {
            await s.DeleteAsync(scenarioId, ct);
            return TypedResults.NoContent();
        }).WithName("DeleteScenario");
        scenarios.MapPut(ScenariosRoutes.TextPattern, (Guid scenarioId, ScenarioTextInput input, HttpRequest request, ScenarioService s, CancellationToken ct) =>
            s.SaveTextAsync(scenarioId, input, HttpIfMatch.Version(request), ct)).WithName("SaveScenarioText");
        scenarios.MapPut(ScenariosRoutes.OrderPattern, async (Guid scenarioId, ReorderRequest input, ScenarioService s, CancellationToken ct) =>
        {
            await s.ReorderAsync(scenarioId, input, ct);
            return TypedResults.NoContent();
        }).WithName("ReorderScenarioPart");

        scenarios.MapPut(ScenariosRoutes.LocationMusicPattern, (Guid scenarioId, Guid locationId, LocationMusicInput input, ScenarioPlayService p,
            CancellationToken ct) => p.SetLocationMusicAsync(scenarioId, locationId, input, ct)).WithName("SetScenarioLocationMusic");
        scenarios.MapGet(ScenariosRoutes.HandoutScreenPattern, async Task<IResult> (Guid scenarioId, Guid handoutId, HttpContext http,
            ScenarioPlayService p, CancellationToken ct) =>
        {
            var handout = await p.GetHandoutScreenAsync(scenarioId, handoutId, ct);
            http.Response.Headers.CacheControl = "private, no-store";
            return TypedResults.Ok(handout);
        }).WithName("GetHandoutScreen");

        scenarios.MapPost(ScenariosRoutes.LocationsPattern, (Guid scenarioId, LocationInput input, ScenarioPartsService p, CancellationToken ct) =>
            p.AddLocationAsync(scenarioId, input, ct)).WithName("AddScenarioLocation");
        scenarios.MapPut(ScenariosRoutes.LocationPattern, (Guid scenarioId, Guid locationId, LocationInput input, ScenarioPartsService p, CancellationToken ct) =>
            p.UpdateLocationAsync(scenarioId, locationId, input, ct)).WithName("UpdateScenarioLocation");
        scenarios.MapDelete(ScenariosRoutes.LocationPattern, async (Guid scenarioId, Guid locationId, ScenarioPartsService p, CancellationToken ct) =>
        {
            await p.DeleteLocationAsync(scenarioId, locationId, ct);
            return TypedResults.NoContent();
        }).WithName("DeleteScenarioLocation");

        scenarios.MapPost(ScenariosRoutes.LocationChecksPattern, (Guid scenarioId, Guid locationId, CheckInput input, ScenarioPartsService p, CancellationToken ct) =>
            p.AddCheckAsync(scenarioId, locationId, input, ct)).WithName("AddScenarioCheck");
        scenarios.MapPut(ScenariosRoutes.CheckPattern, (Guid scenarioId, Guid checkId, CheckInput input, ScenarioPartsService p, CancellationToken ct) =>
            p.UpdateCheckAsync(scenarioId, checkId, input, ct)).WithName("UpdateScenarioCheck");
        scenarios.MapDelete(ScenariosRoutes.CheckPattern, async (Guid scenarioId, Guid checkId, ScenarioPartsService p, CancellationToken ct) =>
        {
            await p.DeleteCheckAsync(scenarioId, checkId, ct);
            return TypedResults.NoContent();
        }).WithName("DeleteScenarioCheck");

        scenarios.MapPost(ScenariosRoutes.FactsPattern, (Guid scenarioId, KeyFactInput input, ScenarioPartsService p, CancellationToken ct) =>
            p.AddFactAsync(scenarioId, input, ct)).WithName("AddScenarioFact");
        scenarios.MapPut(ScenariosRoutes.FactPattern, (Guid scenarioId, Guid factId, KeyFactInput input, ScenarioPartsService p, CancellationToken ct) =>
            p.UpdateFactAsync(scenarioId, factId, input, ct)).WithName("UpdateScenarioFact");
        scenarios.MapDelete(ScenariosRoutes.FactPattern, async (Guid scenarioId, Guid factId, ScenarioPartsService p, CancellationToken ct) =>
        {
            await p.DeleteFactAsync(scenarioId, factId, ct);
            return TypedResults.NoContent();
        }).WithName("DeleteScenarioFact");

        scenarios.MapPost(ScenariosRoutes.HandoutsPattern, (Guid scenarioId, HandoutInput input, ScenarioPartsService p, CancellationToken ct) =>
            p.AddHandoutAsync(scenarioId, input, ct)).WithName("AddScenarioHandout");
        scenarios.MapPut(ScenariosRoutes.HandoutPattern, (Guid scenarioId, Guid handoutId, HandoutInput input, ScenarioPartsService p, CancellationToken ct) =>
            p.UpdateHandoutAsync(scenarioId, handoutId, input, ct)).WithName("UpdateScenarioHandout");
        scenarios.MapDelete(ScenariosRoutes.HandoutPattern, async (Guid scenarioId, Guid handoutId, ScenarioPartsService p, CancellationToken ct) =>
        {
            await p.DeleteHandoutAsync(scenarioId, handoutId, ct);
            return TypedResults.NoContent();
        }).WithName("DeleteScenarioHandout");

        scenarios.MapPost(ScenariosRoutes.CreaturesPattern, (Guid scenarioId, ScenarioCreatureInput input, ScenarioPartsService p, CancellationToken ct) =>
            p.AddCreatureAsync(scenarioId, input, ct)).WithName("AddScenarioCreature");
        scenarios.MapPut(ScenariosRoutes.CreaturePattern, (Guid scenarioId, Guid creatureRowId, ScenarioCreatureInput input, ScenarioPartsService p,
            CancellationToken ct) => p.UpdateCreatureAsync(scenarioId, creatureRowId, input, ct)).WithName("UpdateScenarioCreature");
        scenarios.MapDelete(ScenariosRoutes.CreaturePattern, async (Guid scenarioId, Guid creatureRowId, ScenarioPartsService p, CancellationToken ct) =>
        {
            await p.DeleteCreatureAsync(scenarioId, creatureRowId, ct);
            return TypedResults.NoContent();
        }).WithName("DeleteScenarioCreature");

        scenarios.MapPost(ScenariosRoutes.ItemsPattern, (Guid scenarioId, ScenarioItemInput input, ScenarioPartsService p, CancellationToken ct) =>
            p.AddItemAsync(scenarioId, input, ct)).WithName("AddScenarioItem");
        scenarios.MapPut(ScenariosRoutes.ItemPattern, (Guid scenarioId, Guid itemRowId, ScenarioItemInput input, ScenarioPartsService p, CancellationToken ct) =>
            p.UpdateItemAsync(scenarioId, itemRowId, input, ct)).WithName("UpdateScenarioItem");
        scenarios.MapDelete(ScenariosRoutes.ItemPattern, async (Guid scenarioId, Guid itemRowId, ScenarioPartsService p, CancellationToken ct) =>
        {
            await p.DeleteItemAsync(scenarioId, itemRowId, ct);
            return TypedResults.NoContent();
        }).WithName("DeleteScenarioItem");

        scenarios.MapPut(ScenariosRoutes.NpcPattern, async (Guid scenarioId, Guid characterId, NpcCastInput input, ScenarioCastService c, CancellationToken ct) =>
        {
            await c.CastNpcAsync(scenarioId, characterId, input, ct);
            return TypedResults.NoContent();
        }).WithName("CastScenarioNpc");
        scenarios.MapDelete(ScenariosRoutes.NpcPattern, async (Guid scenarioId, Guid characterId, ScenarioCastService c, CancellationToken ct) =>
        {
            await c.RemoveNpcAsync(scenarioId, characterId, ct);
            return TypedResults.NoContent();
        }).WithName("RemoveScenarioNpc");
        scenarios.MapPost(ScenariosRoutes.PregensPattern, (Guid scenarioId, AddPregenRequest input, ScenarioCastService c, CancellationToken ct) =>
            c.AddPregenAsync(scenarioId, input.PregenId, ct)).WithName("AddScenarioPregen");
        scenarios.MapDelete(ScenariosRoutes.PregenPattern, async (Guid scenarioId, Guid characterId, ScenarioCastService c, CancellationToken ct) =>
        {
            await c.RemovePregenAsync(scenarioId, characterId, ct);
            return TypedResults.NoContent();
        }).WithName("RemoveScenarioPregen");

        ScenarioExchangeEndpoints.Map(scenarios);
        RunEndpoints.Map(group);
        return app;
    }
}
