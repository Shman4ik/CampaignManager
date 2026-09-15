using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CampaignManager.Web.Components.Features.Music.Model;

namespace CampaignManager.Web.Components.Features.Music.Services;

/// <summary>
///     Перенос фонотеки одним JSON-файлом. Пятнадцать треков по одной форме — это полчаса
///     кликов, а подборка всё равно собирается снаружи: в заметках, в чужом плейлисте, в чате.
///     <para>
///         Пишет только через <see cref="MusicTrackService" />, поэтому нормализация тегов,
///         запрет на повтор названий и <c>Init()</c> отрабатывают ровно так же, как при ручном вводе.
///         Устроен по образцу <c>ScenarioImportService</c>.
///     </para>
/// </summary>
public sealed class MusicImportService(
    MusicTrackService trackService,
    ILogger<MusicImportService> logger)
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        // Экспорт — ещё и образец формата, поэтому camelCase и отступы.
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    ///     Заводит треки из JSON. Трек с уже занятым названием <b>пропускается</b>, а не
    ///     переписывается: повторный запуск того же файла ничего не ломает и не плодит двойников.
    /// </summary>
    public async Task<MusicImportResult> ImportAsync(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new MusicImportResult { Success = false, Error = "Пустой JSON." };

        MusicLibraryDto? dto;
        try
        {
            dto = ParseLibrary(json);
        }
        catch (JsonException ex)
        {
            return new MusicImportResult { Success = false, Error = $"JSON не разобран: {ex.Message}" };
        }

        if (dto is null || dto.Tracks.Count == 0)
            return new MusicImportResult { Success = false, Error = "В файле нет ни одного трека." };

        var result = new MusicImportResult { Success = true };
        var existing = (await trackService.GetAllTracksAsync())
            .Select(t => t.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in dto.Tracks)
        {
            if (!TryBuildTrack(item, result.Warnings, out var track))
            {
                result.Skipped++;
                continue;
            }

            if (!existing.Add(track.Name))
            {
                result.Warnings.Add($"«{track.Name}» — трек с таким названием уже есть, пропущен.");
                result.Skipped++;
                continue;
            }

            if (await trackService.CreateTrackAsync(track) is null)
            {
                result.Warnings.Add($"«{track.Name}» — не удалось сохранить.");
                result.Skipped++;
                continue;
            }

            result.Created++;
        }

        logger.LogInformation("Импорт фонотеки: заведено {Created}, пропущено {Skipped}",
            result.Created, result.Skipped);

        return result;
    }

    /// <summary>
    ///     Принимает и объект <c>{ "tracks": [...] }</c>, и голый массив треков — руками
    ///     подборку чаще собирают именно массивом.
    /// </summary>
    private static MusicLibraryDto? ParseLibrary(string json)
    {
        var trimmed = json.TrimStart();
        if (trimmed.StartsWith('['))
            return new MusicLibraryDto { Tracks = JsonSerializer.Deserialize<List<MusicTrackDto>>(json, ReadOptions) ?? [] };

        return JsonSerializer.Deserialize<MusicLibraryDto>(json, ReadOptions);
    }

    private static bool TryBuildTrack(MusicTrackDto dto, List<string> warnings, out MusicTrack track)
    {
        track = null!;

        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            warnings.Add("Трек без поля name пропущен.");
            return false;
        }

        var source = dto.Source?.Trim();
        if (string.IsNullOrWhiteSpace(source))
        {
            warnings.Add($"«{name}» — не заполнено поле source, пропущен.");
            return false;
        }

        // Тип источника обычно виден по самой ссылке; явное поле нужно лишь для
        // объекта хранилища с необычным именем.
        var explicitType = dto.SourceType?.Trim();
        var isStorage = string.Equals(explicitType, "storage", StringComparison.OrdinalIgnoreCase)
                        || (explicitType is null && !LooksLikeYouTube(source));

        if (isStorage)
        {
            if (!MusicSource.IsSupportedAudioFile(source))
            {
                warnings.Add($"«{name}» — «{source}» не похоже ни на ссылку YouTube, ни на звуковой файл, пропущен.");
                return false;
            }

            track = Build(name, MusicSourceType.Storage, source, dto);
            return true;
        }

        if (!MusicSource.TryParseYouTubeId(source, out var videoId))
        {
            warnings.Add($"«{name}» — из «{source}» не удалось выделить идентификатор ролика, пропущен.");
            return false;
        }

        track = Build(name, MusicSourceType.YouTube, videoId, dto);
        return true;
    }

    private static bool LooksLikeYouTube(string source) =>
        source.Contains("youtu", StringComparison.OrdinalIgnoreCase)
        || MusicSource.TryParseYouTubeId(source, out _);

    private static MusicTrack Build(string name, MusicSourceType type, string source, MusicTrackDto dto) => new()
    {
        Name = name,
        SourceType = type,
        Source = source,
        Tags = MusicSource.NormalizeTags(dto.Tags),
        Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
        StartSeconds = Math.Clamp(dto.StartSeconds ?? 0, 0, 86400),
        Loop = dto.Loop ?? true,
        Volume = Math.Clamp(dto.Volume ?? 100, 0, 100)
    };

    /// <summary>Отдаёт всю фонотеку в том же виде, который принимает импорт.</summary>
    public async Task<string> ExportAsync()
    {
        var tracks = await trackService.GetAllTracksAsync();

        var dto = new MusicLibraryDto
        {
            Tracks =
            [
                .. tracks.Select(t => new MusicTrackDto
                {
                    Name = t.Name,
                    // Ссылку собираем обратно: обменный файл должен читаться человеком
                    // и открываться щелчком, а не хранить голый идентификатор.
                    Source = t.SourceType == MusicSourceType.YouTube
                        ? $"https://www.youtube.com/watch?v={t.Source}"
                        : t.Source,
                    SourceType = t.SourceType == MusicSourceType.YouTube ? "youtube" : "storage",
                    Tags = t.Tags,
                    Notes = t.Notes,
                    StartSeconds = t.StartSeconds == 0 ? null : t.StartSeconds,
                    Loop = t.Loop ? null : false,
                    Volume = t.Volume == 100 ? null : t.Volume
                })
            ]
        };

        return JsonSerializer.Serialize(dto, WriteOptions);
    }
}
