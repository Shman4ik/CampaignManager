namespace CampaignManager.UI.KeeperScreen;

/// <summary>Раздел ширмы Хранителя.</summary>
public enum KeeperScreenBlock
{
    Checks,
    Dice,
    Push,
    Luck,
    Damage,
    Firearms,
    Sanity,
    GroupCheck,
    Chase,
}

/// <summary>Вкладка ширмы: раздел, его имя в адресе <c>/reference?block=…</c>, подпись и иконка.</summary>
public sealed record KeeperScreenBlockInfo(KeeperScreenBlock Block, string Slug, string Label, string Icon);

public static class KeeperScreenBlocks
{
    /// <summary>Адрес страницы ширмы.</summary>
    public const string PageUrl = "reference";

    /// <summary>Порядок вкладок: от того, что нужно на каждой проверке, к редким таблицам.</summary>
    public static IReadOnlyList<KeeperScreenBlockInfo> All { get; } =
    [
        new(KeeperScreenBlock.Checks, "checks", "Проверки", "fa-bullseye"),
        new(KeeperScreenBlock.Dice, "dice", "Кости", "fa-dice"),
        new(KeeperScreenBlock.Push, "push", "Повтор", "fa-rotate-right"),
        new(KeeperScreenBlock.Luck, "luck", "Удача", "fa-clover"),
        new(KeeperScreenBlock.Damage, "damage", "Урон", "fa-fire"),
        new(KeeperScreenBlock.Firearms, "firearms", "Стрельба", "fa-crosshairs"),
        new(KeeperScreenBlock.Sanity, "sanity", "Рассудок", "fa-brain"),
        new(KeeperScreenBlock.Chase, "chase", "Погоня", "fa-person-running"),
        new(KeeperScreenBlock.GroupCheck, "group", "Групповая проверка", "fa-users"),
    ];

    /// <summary>Раздел по имени из адреса; незнакомое имя — первый раздел, а не ошибка.</summary>
    public static KeeperScreenBlock FromSlug(string? slug) =>
        All.FirstOrDefault(b => string.Equals(b.Slug, slug, StringComparison.OrdinalIgnoreCase))?.Block
        ?? KeeperScreenBlock.Checks;

    public static string Slug(KeeperScreenBlock block) => All.First(b => b.Block == block).Slug;

    /// <summary>Адрес раздела на отдельной странице — для закладки на планшете.</summary>
    public static string Url(KeeperScreenBlock block) => $"{PageUrl}?block={Slug(block)}";
}
