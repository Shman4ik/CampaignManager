using CampaignManager.Web.Components.Features.Music.Model;
using CampaignManager.Web.Utilities.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace CampaignManager.Web.Components.Features.Music.Services;

/// <summary>
///     Заводит пачку треков из файлов одним действием. Форма трека принимает один файл за раз,
///     и собранная снаружи подборка (папка mp3 с диска) превращалась в десятки одинаковых
///     заполнений формы — ровно та же причина, по которой появился <see cref="MusicImportService" />,
///     только для файлов, а не для ссылок.
///     <para>
///         Пишет только через <see cref="MusicTrackService" />: нормализация тегов, <c>Init()</c>
///         и запрет на повтор названий отрабатывают как при ручном вводе. Отчёт — тот же
///         <see cref="MusicImportResult" />, что у импорта JSON.
///     </para>
/// </summary>
public sealed class MusicBulkUploadService(
    MusicTrackService trackService,
    MinioService minioService,
    ILogger<MusicBulkUploadService> logger)
{
    /// <summary>Потолок одной пачки. Больше сотни за раз — это уже не «залил папку», а импорт.</summary>
    public const int MaxFiles = 100;

    /// <summary>Тот же лимит на файл, что у формы одиночного трека.</summary>
    public const long MaxFileSize = 50L * 1024 * 1024;

    /// <summary>
    ///     Грузит файлы в хранилище и заводит по треку на каждый. Название берётся из имени файла,
    ///     теги общие на всю пачку — папка на диске обычно и собрана по настроению.
    /// </summary>
    /// <param name="files">Что выбрал Хранитель. Порядок сохраняется — так же идёт и отчёт.</param>
    /// <param name="tags">Теги настроения, одни на всю пачку.</param>
    /// <param name="loop">Зациклить заведённые треки.</param>
    /// <param name="progress">Куда сообщать о продвижении; модалка по нему рисует полосу.</param>
    public async Task<MusicImportResult> UploadAsync(
        IReadOnlyList<IBrowserFile> files,
        IEnumerable<string>? tags,
        bool loop = true,
        IProgress<MusicUploadProgress>? progress = null)
    {
        if (files.Count == 0)
            return new MusicImportResult { Success = false, Error = "Не выбрано ни одного файла." };

        var normalizedTags = MusicSource.NormalizeTags(tags);
        var result = new MusicImportResult { Success = true };

        // Занятые названия собираем один раз и пополняем на ходу: внутри пачки тоже бывают
        // двойники (та же мелодия в двух папках), и ловить их повторным запросом в базу незачем.
        var taken = (await trackService.GetAllTracksAsync())
            .Select(t => t.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var done = 0;
        foreach (var file in files)
        {
            progress?.Report(new MusicUploadProgress(done, files.Count, file.Name));

            if (!TryReserveName(file, taken, result, out var name))
            {
                done++;
                continue;
            }

            try
            {
                // Имя объекта случайное, как в форме одиночного трека: имена файлов
                // повторяются, и перезаписать чужой трек из-за совпадения нельзя.
                var extension = Path.GetExtension(file.Name).ToLowerInvariant();
                var objectName = $"music/{Guid.CreateVersion7()}{extension}";

                await using (var stream = file.OpenReadStream(MaxFileSize))
                {
                    await minioService.UploadAsync(stream, objectName, MusicSource.GetAudioContentType(file.Name));
                }

                var track = new MusicTrack
                {
                    Name = name,
                    SourceType = MusicSourceType.Storage,
                    Source = objectName,
                    Tags = normalizedTags,
                    Loop = loop
                };

                if (await trackService.CreateTrackAsync(track) is null)
                {
                    result.Warnings.Add($"«{name}» — не удалось сохранить.");
                    result.Skipped++;
                }
                else
                {
                    result.Created++;
                }
            }
            catch (IOException)
            {
                result.Warnings.Add($"«{file.Name}» — больше 50 МБ, пропущен.");
                result.Skipped++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Не удалось загрузить трек {FileName}", file.Name);
                result.Warnings.Add($"«{file.Name}» — не удалось загрузить.");
                result.Skipped++;
            }

            done++;
        }

        progress?.Report(new MusicUploadProgress(done, files.Count, string.Empty));

        logger.LogInformation("Пакетная загрузка фонотеки: заведено {Created}, пропущено {Skipped}",
            result.Created, result.Skipped);

        return result;
    }

    /// <summary>
    ///     Проверяет файл и занимает под него название. Отказ здесь — единственный способ
    ///     не положить в хранилище объект, на который потом никто не сошлётся.
    /// </summary>
    private static bool TryReserveName(
        IBrowserFile file,
        HashSet<string> taken,
        MusicImportResult result,
        out string name)
    {
        name = Path.GetFileNameWithoutExtension(file.Name).Trim();

        if (!MusicSource.IsSupportedAudioFile(file.Name))
        {
            result.Warnings.Add($"«{file.Name}» — формат не поддерживается, пропущен.");
            result.Skipped++;
            return false;
        }

        if (name.Length == 0)
        {
            result.Warnings.Add($"«{file.Name}» — из имени файла не вышло названия, пропущен.");
            result.Skipped++;
            return false;
        }

        if (!taken.Add(name))
        {
            result.Warnings.Add($"«{name}» — трек с таким названием уже есть, пропущен.");
            result.Skipped++;
            return false;
        }

        return true;
    }
}
