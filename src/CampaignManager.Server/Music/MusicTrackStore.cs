using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Files;
using CampaignManager.Contracts.Music;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Music;
using CampaignManager.Data;
using CampaignManager.Data.Music;
using CampaignManager.Server.Catalogs;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Server.Music;

/// <summary>
/// Фонотека — справочник со своим хранилищем: всё общее (права, ETag, <c>If-Match</c>, дубль имени, импорт,
/// экспорт, журнал правок) делает <see cref="CatalogService{TEntity, TDto}"/>. Своё — источник трека и теги.
/// </summary>
public sealed class MusicTrackStore : CatalogStore<MusicTrack, MusicTrackDto>
{
    /// <summary>Сутки: начать дальше незачем, а опечатка «36000» вместо «360» видна сразу.</summary>
    public const int MaxStartSeconds = 24 * 60 * 60;

    private static readonly CatalogCodeTable NoCodes = new("music.", []);

    public override CatalogRoute Route => MusicRoutes.Tracks;

    public override CatalogCodeTable Codes => NoCodes;

    public override bool HasCodes => false;

    public override string Noun => "Трек";

    public override DbSet<MusicTrack> Set(CmDbContext db) => db.MusicTracks;

    public override MusicTrack New() => new() { Name = "" };

    public override async Task<IReadOnlyList<MusicTrackDto>> ToDtosAsync(CmDbContext db, IReadOnlyList<MusicTrack> rows, CancellationToken cancellationToken)
    {
        var fileIds = rows.Select(r => r.FileId).OfType<Guid>().ToList();
        var names = fileIds.Count == 0
            ? []
            : await db.Files.AsNoTracking().Where(f => fileIds.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => f.OriginalName, cancellationToken);

        return rows.Select(t => new MusicTrackDto
        {
            Id = t.Id,
            Version = t.Version,
            Name = t.Name,
            YoutubeId = t.YoutubeId,
            FileId = t.FileId,
            FileUrl = t.FileId is { } fileId ? FilesRoutes.Content(fileId) : null,
            FileName = t.FileId is { } id ? names.GetValueOrDefault(id) : null,
            StartSeconds = t.StartSeconds,
            Loop = t.Loop,
            Volume = t.Volume,
            Tags = [.. t.Tags],
            Notes = t.Notes,
        }).ToList();
    }

    public override async Task ApplyAsync(CmDbContext db, MusicTrackDto dto, MusicTrack entity, CatalogWrite write, CancellationToken cancellationToken)
    {
        var hasVideo = !string.IsNullOrWhiteSpace(dto.YoutubeId);
        if (hasVideo == dto.FileId is not null)
        {
            throw ApiProblemException.Invalid("Источник трека — ролик YouTube или загруженный файл, ровно одно из двух.");
        }

        string? videoId = null;
        if (hasVideo && !YouTubeVideo.TryParseId(dto.YoutubeId, out videoId))
        {
            throw ApiProblemException.Invalid($"«{dto.YoutubeId!.Trim()}» не похоже на ссылку на ролик YouTube.");
        }

        if (dto.FileId is { } fileId)
        {
            var file = await db.Files.AsNoTracking().Where(f => f.Id == fileId)
                .Select(f => new { f.StorageKey, f.ContentType })
                .SingleOrDefaultAsync(cancellationToken);
            if (file is null)
            {
                throw ApiProblemException.Invalid(write.IsImport
                    ? "Файла трека нет в этой базе: файл обмена ссылается на другую — загрузите звук заново."
                    : "Файл трека не найден — загрузите его заново.");
            }

            // Только загруженный звук: внешний адрес отдаётся переходом на чужой origin, а Web Audio на
            // элементе с чужого origin без CORS даёт тишину (громкость на iPad — только через Web Audio).
            if (file.StorageKey is null || file.ContentType?.StartsWith("audio/", StringComparison.Ordinal) != true)
            {
                throw ApiProblemException.Invalid("Трек — только загруженный звуковой файл (mp3, ogg, m4a…), не картинка и не внешний адрес.");
            }
        }

        var start = Range(dto.StartSeconds, 0, MaxStartSeconds, "Начало, секунды") ?? 0;
        var volume = Range(dto.Volume, 0, 100, "Громкость трека") ?? 100;
        var tags = MusicTags.Normalize(dto.Tags);
        if (tags.Count > MusicTags.MaxCount)
        {
            throw ApiProblemException.Invalid($"Тегов больше {MusicTags.MaxCount} — оставьте главные настроения.");
        }

        if (tags.FirstOrDefault(t => t.Length > MusicTags.MaxLength) is { } longTag)
        {
            throw ApiProblemException.Invalid($"Тег «{longTag}» длиннее {MusicTags.MaxLength} знаков: тег — одно-два слова настроения.");
        }

        entity.YoutubeId = videoId;
        entity.FileId = hasVideo ? null : dto.FileId;
        entity.StartSeconds = start;
        entity.Loop = dto.Loop;
        entity.Volume = volume;
        entity.Tags = tags;
        entity.Notes = Text(dto.Notes);
    }
}
