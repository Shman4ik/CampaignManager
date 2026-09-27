namespace CampaignManager.Web.Components.Features.Characters.Model;

/// <summary>
///     Как разыгрывается приступ безумия (стр. 155–157). Лежит в JSONB числом —
///     значения не переставлять.
/// </summary>
public enum InsanityBoutMode
{
    /// <summary>Таблица VII: рядом другие сыщики, приступ идёт по раундам (1d10 раундов).</summary>
    RealTime = 0,

    /// <summary>Таблица VIII: сыщик один или обезумели все — Хранитель проматывает время (обычно 1d10 часов).</summary>
    Summary = 1
}

/// <summary>
///     Разыгранный приступ безумия — чтобы после закрытия диалога на листе было видно, что
///     выпало и сколько он длится. Текст приступа не хранится: его по номеру отдаёт
///     <c>InsanityTables</c>, копия в JSONB разошлась бы с таблицей при первой же правке.
/// </summary>
public class InsanityBout
{
    public InsanityBoutMode Mode { get; set; }

    /// <summary>Результат 1d10 по таблице VII или VIII.</summary>
    public int Roll { get; set; }

    /// <summary>Длительность в раундах (VII) или часах (VIII); null — Хранитель её не бросал.</summary>
    public int? Duration { get; set; }

    public DateTime RolledAt { get; set; } = DateTime.UtcNow;
}
