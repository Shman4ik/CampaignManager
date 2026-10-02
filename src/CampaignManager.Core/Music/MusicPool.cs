namespace CampaignManager.Core.Music;

/// <summary>
/// Пул сцены: что может заиграть в локации, по кнопке настроения или в бою. Трек попадает в пул, если у
/// него есть хоть один из тегов или он прибит к сцене явно. Одна копия правила: его зовут и сервер
/// (<c>GET /api/v1/music/pool</c>), и плеер в браузере, у которого фонотека уже загружена.
/// </summary>
/// <param name="Tags">Настроения сцены (нормализуются).</param>
/// <param name="TrackIds">Прибитые треки — играют, даже если тегов у них нет.</param>
public sealed record MusicPool(IReadOnlyList<string> Tags, IReadOnlyList<Guid> TrackIds)
{
    public static MusicPool Empty { get; } = new([], []);

    public static MusicPool Of(IEnumerable<string?>? tags, IEnumerable<Guid>? trackIds = null) =>
        new(MusicTags.Normalize(tags), [.. (trackIds ?? []).Distinct()]);

    public static MusicPool OfTrack(Guid trackId) => new([], [trackId]);

    public bool IsEmpty => Tags.Count == 0 && TrackIds.Count == 0;

    /// <summary>Подходит ли трек: прибит или несёт хоть один тег пула. Теги трека — уже нормализованные.</summary>
    public bool Contains(Guid trackId, IEnumerable<string> trackTags) =>
        TrackIds.Contains(trackId) || trackTags.Any(Tags.Contains);

    /// <summary>Тот же пул — по составу, а не по ссылкам на списки (кнопка «играет эта сцена»).</summary>
    public bool Equals(MusicPool? other) =>
        other is not null && Tags.SequenceEqual(other.Tags) && TrackIds.SequenceEqual(other.TrackIds);

    public override int GetHashCode() => HashCode.Combine(Tags.Count, TrackIds.Count, Tags.FirstOrDefault(), TrackIds.FirstOrDefault());

    /// <summary>Треки пула в порядке фонотеки.</summary>
    public List<T> Select<T>(IEnumerable<T> tracks, Func<T, Guid> id, Func<T, IEnumerable<string>> tags) =>
        IsEmpty ? [] : [.. tracks.Where(t => Contains(id(t), tags(t)))];
}

/// <summary>
/// «Случайно, но не одно и то же» (v1, <c>MusicPlaybackService.Pick</c>): из пула вычитаются последние
/// <c>min(3, пул − 1)</c> сыгранных, из остатка — равновероятно. Остаток пуст (пул короче окна) — весь пул,
/// иначе «Другой» на пуле из одного трека перестал бы работать. Подряд повторов нет, а за несколько сцен
/// пул звучит весь.
/// </summary>
public static class MusicShuffle
{
    public const int AntiRepeatWindow = 3;

    /// <summary>Длина хранимой истории — окна хватает с запасом.</summary>
    public const int HistoryLimit = 12;

    /// <param name="pool">Кандидаты; пустой — <c>null</c>.</param>
    /// <param name="history">Сыгранное, последнее — в конце.</param>
    public static T? Pick<T>(IReadOnlyList<T> pool, Func<T, Guid> id, IReadOnlyList<Guid> history, Random random)
        where T : class
    {
        if (pool.Count == 0)
        {
            return null;
        }

        var skip = Math.Min(AntiRepeatWindow, pool.Count - 1);
        var recent = history.Skip(Math.Max(0, history.Count - skip)).ToHashSet();
        var candidates = pool.Where(t => !recent.Contains(id(t))).ToList();
        if (candidates.Count == 0)
        {
            candidates = [.. pool];
        }

        return candidates[random.Next(candidates.Count)];
    }

    /// <summary>История с новым треком, не длиннее <see cref="HistoryLimit"/>.</summary>
    public static List<Guid> Remember(IEnumerable<Guid> history, Guid played) =>
        [.. history.Append(played).TakeLast(HistoryLimit)];
}

/// <summary>Умолчания плеера (v1, <c>MusicPlaybackDefaults</c>).</summary>
public static class MusicDefaults
{
    /// <summary>Громкость при первом запуске: фон под разговор за столом, а не концерт.</summary>
    public const int Volume = 60;

    /// <summary>Настроения на панели плеера, пока Хранитель не закрепил свои.</summary>
    public static IReadOnlyList<string> PinnedTags { get; } = ["бой", "погоня", "напряжение", "расследование", "ужас", "спокойствие"];
}
