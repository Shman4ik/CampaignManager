using CampaignManager.Web.Components.Features.Music.Model;
using Microsoft.AspNetCore.Components;

namespace CampaignManager.Web.Components.Features.Music.Services;

/// <summary>
///     Состояние плеера Хранителя: что играет, из какого пула выбрано, что уже звучало.
///     Сам звук живёт в браузере (<c>wwwroot/js/music-player.js</c>) — сервис только решает,
///     какой трек следующий, и хранит это решение так, чтобы оно пережило паузу circuit.
///     Устроен как <c>CombatService</c>/<c>ChaseService</c>: событие <see cref="OnChange" />
///     вместо параметров и один снимок под <c>[PersistentState]</c>.
/// </summary>
public sealed class MusicPlaybackService(
    MusicTrackService trackService,
    ILogger<MusicPlaybackService> logger)
{
    /// <summary>
    ///     Сколько последних треков пула не предлагать повторно. Ровно та «случайность, но
    ///     не одно и то же», ради которой фича и затевалась: подряд повторов нет, а за
    ///     несколько сцен пул успевает прозвучать весь.
    /// </summary>
    private const int AntiRepeatWindow = 3;

    /// <summary>Длина хранимой истории — окна хватает с запасом, снимок не должен пухнуть.</summary>
    private const int HistoryLimit = 12;

    private List<string> _poolTags = [];
    private List<Guid> _poolTrackIds = [];
    private List<Guid> _history = [];

    /// <summary>Трек, который сейчас заряжен в плеер. <c>null</c> — плеер пуст.</summary>
    public MusicTrack? CurrentTrack { get; private set; }

    /// <summary>Играет ли прямо сейчас (в отличие от «заряжен, но на паузе»).</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>Человеческое имя источника трека: «Склад Уэйтли» или «бой».</summary>
    public string? PoolLabel { get; private set; }

    public int Volume { get; private set; } = MusicPlaybackDefaults.Volume;

    public bool Muted { get; private set; }

    /// <summary>Последняя ошибка воспроизведения — например, ролик запрещён к встраиванию.</summary>
    public string? Error { get; private set; }

    /// <summary>
    ///     Растёт при каждой команде «заряди этот трек». Панель плеера сравнивает номер с тем,
    ///     что уже отправила в JS, и по расхождению понимает, что пора перезаряжать, — иначе
    ///     пришлось бы отличать «сменился трек» от «подвинули громкость» по содержимому.
    /// </summary>
    public long CommandVersion { get; private set; }

    /// <summary>Есть ли из чего выбрать следующий трек — по нему включается кнопка «Другой».</summary>
    public bool HasPool => _poolTags.Count > 0 || _poolTrackIds.Count > 0;

    public event Action? OnChange;

    // ───────────────────────── Запуск ─────────────────────────

    /// <summary>
    ///     Заряжает случайный трек из пула сцены. Возвращает <c>false</c>, если под теги
    ///     и закреплённые треки ничего не нашлось — вызывающему есть что показать Хранителю.
    /// </summary>
    public async Task<bool> PlayPoolAsync(
        IEnumerable<string>? tags,
        IEnumerable<Guid>? trackIds,
        string? label)
    {
        _poolTags = MusicSource.NormalizeTags(tags);
        _poolTrackIds = [.. (trackIds ?? []).Distinct()];
        PoolLabel = label;

        return await RollAsync();
    }

    /// <summary>Быстрая кнопка настроения: «бой», «погоня».</summary>
    public Task<bool> PlayTagAsync(string tag) =>
        PlayPoolAsync([tag], null, MusicSource.NormalizeTag(tag));

    /// <summary>
    ///     Играет конкретный трек. Пул схлопывается до него одного, так что «Другой»
    ///     осмысленно гаснет: Хранитель выбрал именно эту вещь.
    /// </summary>
    public async Task<bool> PlayTrackAsync(Guid trackId, string? label = null)
    {
        var track = (await trackService.GetAllTracksAsync()).FirstOrDefault(t => t.Id == trackId);
        if (track is null)
        {
            logger.LogWarning("Трек {TrackId} не найден в фонотеке", trackId);
            return false;
        }

        _poolTags = [];
        _poolTrackIds = [trackId];
        PoolLabel = label ?? track.Name;
        Load(track);
        return true;
    }

    /// <summary>Перебросить трек внутри текущего пула — та же сцена, другая музыка.</summary>
    public Task<bool> RollAsync() => RollInternalAsync();

    private async Task<bool> RollInternalAsync()
    {
        var pool = await trackService.ResolvePoolAsync(_poolTags, _poolTrackIds);
        if (pool.Count == 0)
        {
            Error = "Под это настроение в фонотеке пока ничего нет.";
            NotifyStateChanged();
            return false;
        }

        Load(Pick(pool));
        return true;
    }

    /// <summary>
    ///     Равновероятный выбор из пула за вычетом недавно звучавшего. Если после вычитания
    ///     не осталось никого (пул короче окна), берём из всего пула — иначе кнопка «Другой»
    ///     на пуле из одного трека просто перестала бы работать.
    /// </summary>
    private MusicTrack Pick(List<MusicTrack> pool)
    {
        var skip = Math.Min(AntiRepeatWindow, pool.Count - 1);
        var recent = _history.TakeLast(skip).ToHashSet();

        var candidates = pool.Where(t => !recent.Contains(t.Id)).ToList();
        if (candidates.Count == 0) candidates = pool;

        return candidates[Random.Shared.Next(candidates.Count)];
    }

    private void Load(MusicTrack track)
    {
        CurrentTrack = track;
        IsPlaying = true;
        Error = null;
        CommandVersion++;

        _history.Add(track.Id);
        if (_history.Count > HistoryLimit)
            _history = [.. _history.TakeLast(HistoryLimit)];

        NotifyStateChanged();
    }

    // ───────────────────────── Управление ─────────────────────────

    public void Pause()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        NotifyStateChanged();
    }

    public void Resume()
    {
        if (CurrentTrack is null || IsPlaying) return;
        IsPlaying = true;
        NotifyStateChanged();
    }

    public void TogglePause()
    {
        if (IsPlaying) Pause();
        else Resume();
    }

    public void Stop()
    {
        CurrentTrack = null;
        IsPlaying = false;
        PoolLabel = null;
        _poolTags = [];
        _poolTrackIds = [];
        Error = null;
        CommandVersion++;
        NotifyStateChanged();
    }

    public void SetVolume(int volume)
    {
        Volume = Math.Clamp(volume, 0, 100);
        if (Volume > 0) Muted = false;
        NotifyStateChanged();
    }

    public void SetMuted(bool muted)
    {
        Muted = muted;
        NotifyStateChanged();
    }

    public void ToggleMuted() => SetMuted(!Muted);

    /// <summary>
    ///     Трек доиграл. Зацикленный сюда не приходит (его крутит сам браузер), а
    ///     одноразовый уводит сцену на следующий случайный трек того же пула.
    /// </summary>
    public async Task HandleTrackEndedAsync()
    {
        if (CurrentTrack is null) return;

        if (HasPool && !CurrentTrack.Loop)
        {
            await RollInternalAsync();
            return;
        }

        IsPlaying = false;
        NotifyStateChanged();
    }

    /// <summary>Плеер в браузере не смог воспроизвести трек — показываем это Хранителю.</summary>
    public void ReportError(string message)
    {
        Error = message;
        IsPlaying = false;
        logger.LogWarning("Не удалось воспроизвести трек {TrackName}: {Message}",
            CurrentTrack?.Name, message);
        NotifyStateChanged();
    }

    public void ClearError()
    {
        if (Error is null) return;
        Error = null;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    // ───────────────── Сохранение и восстановление ─────────────────

    /// <summary>
    ///     Точка, за которую Blazor держит состояние плеера. Геттер вызывается при постановке
    ///     circuit на паузу, сеттер — при возобновлении. Сам звук паузу переживает своими
    ///     силами (аудио-элемент живёт вне дерева Blazor, см. <c>music-player.js</c>) —
    ///     снимок нужен, чтобы панель после возобновления показывала верный трек и не
    ///     перезаряжала его поверх играющего.
    /// </summary>
    [PersistentState]
    public MusicPlaybackSnapshot? PersistedState
    {
        get => CurrentTrack is null && !HasPool ? null : CreateSnapshot();
        set
        {
            if (value is not null)
                RestoreSnapshot(value);
        }
    }

    public MusicPlaybackSnapshot CreateSnapshot() => new()
    {
        TrackId = CurrentTrack?.Id,
        PoolTags = _poolTags,
        PoolTrackIds = _poolTrackIds,
        PoolLabel = PoolLabel,
        History = _history,
        Volume = Volume,
        Muted = Muted,
        IsPlaying = IsPlaying
    };

    /// <summary>
    ///     Восстановление синхронное — сеттер <c>[PersistentState]</c> await'ить нечего.
    ///     Поэтому сюда кладётся всё, кроме самой сущности трека: её панель плеера дотянет
    ///     из фонотеки в <c>OnInitializedAsync</c> через <see cref="RestoreTrackAsync" />.
    /// </summary>
    private void RestoreSnapshot(MusicPlaybackSnapshot snapshot)
    {
        _poolTags = snapshot.PoolTags;
        _poolTrackIds = snapshot.PoolTrackIds;
        _history = snapshot.History;
        PoolLabel = snapshot.PoolLabel;
        Volume = snapshot.Volume;
        Muted = snapshot.Muted;
        IsPlaying = snapshot.IsPlaying;
        RestoredTrackId = snapshot.TrackId;
    }

    /// <summary>Трек из снимка, который ещё не подгружен из базы. См. <see cref="RestoreTrackAsync" />.</summary>
    public Guid? RestoredTrackId { get; private set; }

    /// <summary>
    ///     Догружает сущность трека после возобновления circuit. Намеренно **не** трогает
    ///     <see cref="CommandVersion" />: трек в браузере уже играет, перезаряжать его
    ///     значило бы оборвать музыку ровно в тот момент, ради которого всё и затевалось.
    /// </summary>
    public async Task RestoreTrackAsync()
    {
        if (RestoredTrackId is not { } id) return;

        RestoredTrackId = null;
        CurrentTrack = (await trackService.GetAllTracksAsync()).FirstOrDefault(t => t.Id == id);
        NotifyStateChanged();
    }
}
