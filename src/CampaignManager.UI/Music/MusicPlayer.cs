using CampaignManager.Contracts.Music;
using CampaignManager.Core.Music;

namespace CampaignManager.UI.Music;

/// <summary>
/// Состояние плеера Хранителя: что играет, из какого пула, что уже звучало, громкость, открыта ли панель.
/// Синглтон WebAssembly — одна вкладка браузера, поэтому плеер переживает переходы между страницами сам,
/// без снимков и восстановления (в v1 — <c>MusicPlaybackService</c> с <c>[PersistentState]</c> на паузу circuit).
/// Звук живёт в <c>MusicPlayerBar.razor.js</c>; здесь только решения, их доводит до браузера панель.
/// <para>
/// Звук играет только на устройстве Хранителя: игра идёт очно, музыка — из колонки рядом с iPad, вещания
/// игрокам нет.
/// </para>
/// </summary>
public sealed class MusicPlayer(Random random)
{
    private List<Guid> _history = [];

    public MusicPlayer()
        : this(Random.Shared)
    {
    }

    /// <summary>Вся фонотека — из справочника (список целиком, ETag). Пул и «Другой» считаются по ней.</summary>
    public IReadOnlyList<MusicTrackDto> Library { get; private set; } = [];

    public bool LibraryLoaded { get; private set; }

    /// <summary>Настроения на панели: свои или умолчания (<see cref="MusicDefaults.PinnedTags"/>).</summary>
    public IReadOnlyList<string> PinnedTags { get; private set; } = MusicDefaults.PinnedTags;

    public bool PinnedTagsAreDefault { get; private set; } = true;

    /// <summary>Заряженный трек; <c>null</c> — плеер пуст.</summary>
    public MusicTrackDto? Current { get; private set; }

    /// <summary>Играет (а не «заряжен, но на паузе»).</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>Откуда трек для человека: «бой», «Склад Уэйтли».</summary>
    public string? PoolLabel { get; private set; }

    public MusicPool Pool { get; private set; } = MusicPool.Empty;

    public int Volume { get; private set; } = MusicDefaults.Volume;

    public bool Muted { get; private set; }

    /// <summary>Ошибка воспроизведения: ролик запрещён к встраиванию, файл не загрузился, пул пуст.</summary>
    public string? Error { get; private set; }

    /// <summary>Браузер не дал запустить звук сам — нужна кнопка «Включить звук» (жест пользователя).</summary>
    public bool NeedsGesture { get; private set; }

    /// <summary>
    /// Панель видна. По умолчанию скрыта — пустая полоса внизу каждой страницы только отнимала место (v1).
    /// Открывает её кнопка «Музыка» в шапке и любой запуск трека. Скрытая панель звук не останавливает.
    /// </summary>
    public bool IsPanelOpen { get; private set; }

    /// <summary>
    /// Растёт на каждой команде «заряди трек»: панель сравнивает его с отправленным в браузер и понимает,
    /// что пора перезаряжать, а не «подвинули громкость».
    /// </summary>
    public long CommandVersion { get; private set; }

    /// <summary>Есть из чего выбрать другой трек — кнопка «Другой».</summary>
    public bool CanRoll => Resolve(Pool).Count > 1;

    public event Action? Changed;

    // ── Фонотека и настройки ──

    /// <summary>Список справочника загружен или изменился (страница фонотеки, панель).</summary>
    public void SetLibrary(IReadOnlyList<MusicTrackDto> tracks)
    {
        Library = tracks;
        LibraryLoaded = true;
        // Трек поправили на странице фонотеки — панель показывает новое имя; звук не перезаряжаем.
        if (Current is { } current && tracks.FirstOrDefault(t => t.Id == current.Id) is { } fresh)
        {
            Current = fresh;
        }

        Notify();
    }

    public void SetPinnedTags(PinnedTagsDto pinned)
    {
        PinnedTags = pinned.Tags;
        PinnedTagsAreDefault = pinned.IsDefault;
        Notify();
    }

    /// <summary>Треки пула по загруженной фонотеке (правило <see cref="MusicPool"/>).</summary>
    public List<MusicTrackDto> Resolve(MusicPool pool) => pool.Select(Library, t => t.Id, t => t.Tags);

    // ── Запуск ──

    /// <summary>Случайный трек пула (локация, кнопка настроения). <c>false</c> — под пул ничего нет.</summary>
    public bool PlayPool(MusicPool pool, string? label)
    {
        Pool = pool;
        PoolLabel = label;
        return Roll();
    }

    /// <summary>Кнопка настроения: «бой», «погоня».</summary>
    public bool PlayTag(string tag) => PlayPool(MusicPool.Of([tag]), MusicTags.Normalize(tag));

    /// <summary>Конкретный трек: пул схлопывается до него, «Другой» гаснет — Хранитель выбрал именно эту вещь.</summary>
    public void PlayTrack(MusicTrackDto track, string? label = null)
    {
        Pool = MusicPool.OfTrack(track.Id);
        PoolLabel = label;
        Load(track);
    }

    /// <summary>Другой трек того же пула — та же сцена, другая музыка.</summary>
    public bool Roll()
    {
        var pool = Resolve(Pool);
        var pick = MusicShuffle.Pick(pool, t => t.Id, _history, random);
        if (pick is null)
        {
            Error = "Под это настроение в фонотеке пока ничего нет.";
            IsPanelOpen = true;
            Notify();
            return false;
        }

        Load(pick);
        return true;
    }

    private void Load(MusicTrackDto track)
    {
        Current = track;
        IsPlaying = true;
        IsPanelOpen = true;
        Error = null;
        NeedsGesture = false;
        CommandVersion++;
        _history = MusicShuffle.Remember(_history, track.Id);
        Notify();
    }

    // ── Управление ──

    public void TogglePause()
    {
        if (Current is null)
        {
            return;
        }

        IsPlaying = !IsPlaying;
        Notify();
    }

    public void Stop()
    {
        Current = null;
        IsPlaying = false;
        PoolLabel = null;
        Pool = MusicPool.Empty;
        Error = null;
        NeedsGesture = false;
        CommandVersion++;
        Notify();
    }

    public void SetVolume(int volume)
    {
        Volume = Math.Clamp(volume, 0, 100);
        if (Volume > 0)
        {
            Muted = false;
        }

        Notify();
    }

    public void ToggleMuted()
    {
        Muted = !Muted;
        Notify();
    }

    /// <summary>Громкость из прошлого раза (браузер помнит её сам), без уведомления — до первой отрисовки.</summary>
    public void RestoreVolume(int volume, bool muted)
    {
        Volume = Math.Clamp(volume, 0, 100);
        Muted = muted;
    }

    public void TogglePanel()
    {
        IsPanelOpen = !IsPanelOpen;
        Notify();
    }

    public void ClosePanel()
    {
        if (!IsPanelOpen)
        {
            return;
        }

        IsPanelOpen = false;
        Notify();
    }

    // ── Что сообщает браузер ──

    /// <summary>
    /// Трек доиграл. Зацикленный сюда не приходит (его крутит браузер); одноразовый уводит сцену на
    /// следующий трек того же пула, а если выбирать не из чего — плеер встаёт на паузу.
    /// </summary>
    public void TrackEnded()
    {
        if (Current is null)
        {
            return;
        }

        if (!Current.Loop && CanRoll)
        {
            Roll();
            return;
        }

        IsPlaying = false;
        Notify();
    }

    public void ReportError(string message)
    {
        Error = message;
        IsPlaying = false;
        Notify();
    }

    public void ClearError()
    {
        if (Error is null)
        {
            return;
        }

        Error = null;
        Notify();
    }

    public void SetNeedsGesture(bool needed)
    {
        NeedsGesture = needed;
        if (needed)
        {
            // Кнопку «Включить звук» нужно увидеть, даже если панель успели скрыть.
            IsPanelOpen = true;
        }

        Notify();
    }

    private void Notify() => Changed?.Invoke();
}
