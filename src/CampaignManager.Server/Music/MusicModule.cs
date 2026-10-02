using System.Text.Json;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Music;
using CampaignManager.Core.Music;
using CampaignManager.Data.Music;
using CampaignManager.Server.Catalogs;
using CampaignManager.Server.Platform;

namespace CampaignManager.Server.Music;

/// <summary>
/// Фонотека Хранителя: треки — справочник на общем сервисе (<c>/api/v1/catalogs/music</c>), сверх него —
/// закреплённые настроения и пул сцены (<c>/api/v1/music/…</c>). Знание модуля — <c>Music/CLAUDE.md</c>.
/// </summary>
public static class MusicModule
{
    public static WebApplicationBuilder AddMusicModule(this WebApplicationBuilder builder)
    {
        CatalogsModule.AddCatalog<MusicTrack, MusicTrackDto, MusicTrackStore>(builder.Services);
        builder.Services.AddScoped<MusicService>();
        return builder;
    }

    public static IEndpointRouteBuilder MapMusicApi(this IEndpointRouteBuilder app)
    {
        CatalogsModule.MapCatalog<MusicTrack, MusicTrackDto>(app, ContractsJsonContext.Default.CatalogFileMusicTrackDto);

        var group = app.MapGroup(ApiRoutes.Prefix + "/music").RequireAuthorization().WithTags("Music");

        group.MapGet("pinned-tags", async (MusicService music, CancellationToken cancellationToken) =>
                TypedResults.Ok(await music.GetPinnedTagsAsync(cancellationToken)))
            .WithName("GetPinnedTags");

        group.MapPut("pinned-tags", async Task<IResult> (HttpRequest request, MusicService music, CancellationToken cancellationToken) =>
            {
                PinnedTagsRequest? body;
                try
                {
                    body = await JsonSerializer.DeserializeAsync(request.Body, ContractsJsonContext.Default.PinnedTagsRequest, cancellationToken);
                }
                catch (JsonException ex)
                {
                    throw ApiProblemException.Invalid($"Не читается как список настроений: {ex.Message}");
                }

                return TypedResults.Ok(await music.SetPinnedTagsAsync(body ?? new PinnedTagsRequest([]), cancellationToken));
            })
            .WithName("SetPinnedTags")
            .Accepts<PinnedTagsRequest>("application/json");

        group.MapGet("pool", async (HttpRequest request, MusicService music, CancellationToken cancellationToken) =>
            {
                var tracks = request.Query[MusicRoutes.TrackQuery]
                    .Select(v => Guid.TryParse(v, out var id) ? id : (Guid?)null)
                    .OfType<Guid>();
                var pool = MusicPool.Of(request.Query[MusicRoutes.TagQuery], tracks);
                return TypedResults.Ok(await music.GetPoolAsync(pool, cancellationToken));
            })
            .WithName("GetMusicPool");

        return app;
    }
}
