using CampaignManager.Contracts.Characters;
using CampaignManager.Core.Characters;
using CampaignManager.Server.Platform;

namespace CampaignManager.Server.Characters;

/// <summary>Лист сыщика. Знание модуля — <c>Characters/CLAUDE.md</c>.</summary>
public static class CharactersModule
{
    private const string Tag = "Characters";

    public static WebApplicationBuilder AddCharactersModule(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<CharacterService>();
        builder.Services.AddScoped<CharacterLibraryService>();
        builder.Services.AddScoped<CharacterDraftService>();
        return builder;
    }

    /// <summary>Все эндпоинты — только вошедшим; кто что может, решает сервис (<c>AccessPolicy</c>).</summary>
    public static IEndpointRouteBuilder MapCharactersApi(this IEndpointRouteBuilder app)
    {
        app.MapPost(CharactersRoutes.Characters, async Task<IResult> (CreateCharacterRequest body, CharacterLibraryService library,
                CancellationToken ct) =>
            {
                var created = await library.CreateAsync(body, ct);
                return TypedResults.Created(CharactersRoutes.Character(created.Id), created);
            })
            .RequireAuthorization().WithName("CreateCharacter").WithTags(Tag);

        app.MapGet(CharactersRoutes.Characters, (CharacterKind kind, bool? archived, CharacterLibraryService library, CancellationToken ct) =>
                library.ListAsync(kind, archived ?? false, ct))
            .RequireAuthorization().WithName("ListCharacterLibrary").WithTags(Tag);

        app.MapGet(CharactersRoutes.NewPattern, (CharacterKind kind, Guid? campaignId, Guid? scenarioId, CharacterLibraryService library,
                CancellationToken ct) => library.GetCreationContextAsync(kind, campaignId, scenarioId, ct))
            .RequireAuthorization().WithName("GetCharacterCreationContext").WithTags(Tag);

        app.MapGet(CharactersRoutes.CharacterPattern, async Task<IResult> (Guid characterId, HttpContext http, CharacterService characters,
                CancellationToken ct) =>
            {
                var character = await characters.GetAsync(characterId, ct);
                // Лист меняется с другого устройства: браузер не должен показывать копию из кэша.
                http.Response.Headers.CacheControl = "private, no-store";
                http.Response.Headers.ETag = $"\"{character.Version}\"";
                return TypedResults.Ok(character);
            })
            .RequireAuthorization().WithName("GetCharacter").WithTags(Tag);

        app.MapPut(CharactersRoutes.SheetPattern, (Guid characterId, CharacterSheet sheet, HttpRequest request, CharacterService characters,
                CancellationToken ct) => characters.SaveSheetAsync(characterId, sheet, HttpIfMatch.Version(request), ct))
            .RequireAuthorization().WithName("SaveCharacterSheet").WithTags(Tag);

        app.MapPut(CharactersRoutes.PortraitPattern, (Guid characterId, SetPortraitRequest body, HttpRequest request, CharacterService characters,
                CancellationToken ct) => characters.SetPortraitAsync(characterId, body.FileId, HttpIfMatch.Version(request), ct))
            .RequireAuthorization().WithName("SetCharacterPortrait").WithTags(Tag);

        app.MapPut(CharactersRoutes.StatusPattern, (Guid characterId, SetStatusRequest body, HttpRequest request, CharacterService characters,
                CancellationToken ct) => characters.SetStatusAsync(characterId, body.Status, HttpIfMatch.Version(request), ct))
            .RequireAuthorization().WithName("SetCharacterStatus").WithTags(Tag);

        app.MapGet(CharactersRoutes.PartyPattern, (Guid characterId, CharacterService characters, CancellationToken ct) =>
                characters.GetPartyAsync(characterId, ct))
            .RequireAuthorization().WithName("GetCharacterParty").WithTags(Tag);

        app.MapGet(CharactersRoutes.CampaignInvestigatorsPattern, (Guid campaignId, CharacterService characters, CancellationToken ct) =>
                characters.GetCampaignInvestigatorsAsync(campaignId, ct))
            .RequireAuthorization().WithName("GetCampaignInvestigators").WithTags(Tag);

        // Черновик помощника: свой — читать, писать (If-Match со второй записи), стереть; игрока — Хранителю кампании.
        app.MapGet(CharacterDraftsRoutes.MinePattern, async Task<IResult> (Guid campaignId, HttpContext http, CharacterDraftService drafts,
                CancellationToken ct) =>
            {
                var draft = await drafts.GetMineAsync(campaignId, ct);
                // Черновик продолжают с другого устройства: копия из кэша браузера увела бы назад.
                http.Response.Headers.CacheControl = "private, no-store";
                http.Response.Headers.ETag = $"\"{draft.Version}\"";
                return TypedResults.Ok(draft);
            })
            .RequireAuthorization().WithName("GetMyCharacterDraft").WithTags(Tag);

        app.MapPut(CharacterDraftsRoutes.MinePattern, (Guid campaignId, InvestigatorDraft draft, HttpRequest request, CharacterDraftService drafts,
                CancellationToken ct) => drafts.SaveMineAsync(campaignId, draft, HttpIfMatch.Version(request), ct))
            .RequireAuthorization().WithName("SaveMyCharacterDraft").WithTags(Tag);

        app.MapDelete(CharacterDraftsRoutes.MinePattern, async Task<IResult> (Guid campaignId, CharacterDraftService drafts, CancellationToken ct) =>
            {
                await drafts.DeleteMineAsync(campaignId, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization().WithName("DeleteMyCharacterDraft").WithTags(Tag);

        app.MapGet(CharacterDraftsRoutes.PlayerPattern, async Task<IResult> (Guid campaignId, Guid userId, HttpContext http, CharacterDraftService drafts,
                CancellationToken ct) =>
            {
                var draft = await drafts.GetAsync(campaignId, userId, ct);
                http.Response.Headers.CacheControl = "private, no-store";
                return TypedResults.Ok(draft);
            })
            .RequireAuthorization().WithName("GetCharacterDraft").WithTags(Tag);

        return app;
    }
}
