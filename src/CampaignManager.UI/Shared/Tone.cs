namespace CampaignManager.UI.Shared;

/// <summary>
/// Смысловой тон сообщения или метки — один набор на Alert, Badge и Toast. Цвет выбирается по
/// смыслу, а не для разнообразия: ошибка и опасность — <see cref="Error"/>, внимание —
/// <see cref="Warning"/>, успех — <see cref="Success"/>, информация и выделение —
/// <see cref="Info"/> (сталь, палитра accent). Отдельной палитры info в 2.0 нет: в v1 она повторяла
/// accent hex-в-hex.
/// </summary>
public enum Tone
{
    Info,
    Success,
    Warning,
    Error,

    /// <summary>Графит — нейтральная метка («Черновик», «Ваншот»).</summary>
    Neutral,

    /// <summary>Тёплый камень — вторая краска рядом с Info (Рассудок рядом с ПМ).</summary>
    Stone,
}

/// <summary>Классы тонов — целыми литералами: Tailwind и поиск по коду видят только их.</summary>
internal static class ToneClasses
{
    public static string Alert(Tone tone) => tone switch
    {
        Tone.Success => "cm-alert-success",
        Tone.Warning => "cm-alert-warning",
        Tone.Error => "cm-alert-error",
        Tone.Neutral => "cm-alert-neutral",
        Tone.Stone => "cm-alert-stone",
        _ => "cm-alert-accent",
    };

    public static string Badge(Tone tone) => tone switch
    {
        Tone.Success => "cm-badge-success",
        Tone.Warning => "cm-badge-warning",
        Tone.Error => "cm-badge-error",
        Tone.Neutral => "cm-badge-neutral",
        Tone.Stone => "cm-badge-stone",
        _ => "cm-badge-accent",
    };

    public static string Icon(Tone tone) => tone switch
    {
        Tone.Success => "fa-circle-check",
        Tone.Warning => "fa-triangle-exclamation",
        Tone.Error => "fa-circle-exclamation",
        _ => "fa-circle-info",
    };
}
