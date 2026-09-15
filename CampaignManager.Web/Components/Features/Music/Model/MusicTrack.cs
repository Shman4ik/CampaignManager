using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Features.Music.Model;

/// <summary>
///     Запись в фонотеке Хранителя: ссылка на ролик YouTube или файл в хранилище,
///     размеченная тегами настроения («бой», «напряжение», «склад»).
/// </summary>
public sealed class MusicTrack : BaseDataBaseEntity, INamedEntity
{
    /// <summary>Название, под которым трек виден в библиотеке и в панели плеера.</summary>
    public required string Name { get; set; }

    /// <summary>Откуда играть — см. <see cref="MusicSourceType" />.</summary>
    public MusicSourceType SourceType { get; set; } = MusicSourceType.YouTube;

    /// <summary>
    ///     Идентификатор ролика YouTube либо имя объекта в хранилище. Ссылку целиком здесь не храним:
    ///     у YouTube их полдюжины форматов, разбираем на входе через <see cref="MusicSource.TryParseYouTubeId" />.
    /// </summary>
    public required string Source { get; set; }

    /// <summary>
    ///     Теги настроения. Нормализованы через <see cref="MusicSource.NormalizeTag" />, иначе «Бой»,
    ///     «бой » и «бой» разошлись бы по трём разным пулам. Колонка jsonb.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Свободная заметка Хранителя: откуда трек, к чему подходит.</summary>
    public string? Notes { get; set; }

    /// <summary>С какой секунды начинать — чтобы проскочить интро или разговор перед музыкой.</summary>
    public int StartSeconds { get; set; }

    /// <summary>
    ///     Зациклить. Эмбиент под сцену обычно да; если нет — по окончании плеер сам берёт
    ///     следующий случайный трек из того же пула.
    /// </summary>
    public bool Loop { get; set; } = true;

    /// <summary>
    ///     Поправка громкости трека, 0..100. Нужна потому, что ролики YouTube сведены
    ///     кто во что горазд. Для файлов применяется через Web Audio, для YouTube —
    ///     через <c>setVolume</c> плеера (на iPad игнорируется, см. CLAUDE.md фичи).
    /// </summary>
    public int Volume { get; set; } = 100;
}
