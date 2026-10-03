using CampaignManager.Contracts.Encounters;
using CampaignManager.Core.Encounters;
using CampaignManager.Server.Platform;

namespace CampaignManager.Server.Encounters;

/// <summary>Сцены: бой и погоня. Знание модуля — <c>Encounters/CLAUDE.md</c>.</summary>
public static class EncountersModule
{
    private const string Tag = "Encounters";

    public static WebApplicationBuilder AddEncountersModule(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<EncounterService>();
        return builder;
    }

    /// <summary>Все эндпоинты — только вошедшим; кто что может, решает сервис (<c>AccessPolicy</c>).</summary>
    public static IEndpointRouteBuilder MapEncountersApi(this IEndpointRouteBuilder app)
    {
        app.MapGet(EncountersRoutes.Encounters, (EncounterKind? kind, EncounterService encounters, CancellationToken ct) =>
                encounters.ListActiveAsync(kind, ct))
            .RequireAuthorization().WithName("ListActiveEncounters").WithTags(Tag);

        app.MapGet(EncountersRoutes.Finished, (EncounterKind? kind, int? take, EncounterService encounters, CancellationToken ct) =>
                encounters.ListFinishedAsync(kind, take ?? 10, ct))
            .RequireAuthorization().WithName("ListFinishedEncounters").WithTags(Tag);

        app.MapPost(EncountersRoutes.Encounters, async Task<IResult> (StartEncounterRequest body, EncounterService encounters,
                CancellationToken ct) =>
            {
                var started = await encounters.StartAsync(body, ct);
                return TypedResults.Created(EncountersRoutes.Encounter(started.Id), started);
            })
            .RequireAuthorization().WithName("StartEncounter").WithTags(Tag);

        app.MapGet(EncountersRoutes.EncounterPattern, async Task<IResult> (Guid encounterId, HttpContext http, EncounterService encounters,
                CancellationToken ct) =>
            {
                var encounter = await encounters.GetAsync(encounterId, ct);
                // Сцену ведут с двух устройств: копия из кэша браузера показала бы прошлый раунд.
                http.Response.Headers.CacheControl = "private, no-store";
                http.Response.Headers.ETag = $"\"{encounter.Version}\"";
                return TypedResults.Ok(encounter);
            })
            .RequireAuthorization().WithName("GetEncounter").WithTags(Tag);

        app.MapPut(EncountersRoutes.StatePattern, (Guid encounterId, EncounterState state, HttpRequest request, EncounterService encounters,
                CancellationToken ct) => encounters.SaveStateAsync(encounterId, state, HttpIfMatch.Version(request), ct))
            .RequireAuthorization().WithName("SaveEncounterState").WithTags(Tag);

        app.MapPut(EncountersRoutes.RunPattern, (Guid encounterId, SetEncounterRunRequest body, HttpRequest request, EncounterService encounters,
                CancellationToken ct) => encounters.SetRunAsync(encounterId, body.RunId, HttpIfMatch.Version(request), ct))
            .RequireAuthorization().WithName("SetEncounterRun").WithTags(Tag);

        app.MapPost(EncountersRoutes.FinishPattern, (Guid encounterId, HttpRequest request, EncounterService encounters,
                CancellationToken ct) => encounters.FinishAsync(encounterId, HttpIfMatch.Version(request), ct))
            .RequireAuthorization().WithName("FinishEncounter").WithTags(Tag);

        return app;
    }
}
