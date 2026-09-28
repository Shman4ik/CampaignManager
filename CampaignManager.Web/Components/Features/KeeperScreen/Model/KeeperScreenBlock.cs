namespace CampaignManager.Web.Components.Features.KeeperScreen.Model;

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
    GroupCheck
}

/// <summary>Вкладка ширмы: раздел, его имя в адресе <c>/reference?block=…</c>, подпись и иконка.</summary>
public sealed record KeeperScreenBlockInfo(KeeperScreenBlock Block, string Slug, string Label, string Icon);

public static class KeeperScreenBlocks
{
    /// <summary>Порядок вкладок: от того, что нужно на каждой проверке, к редким таблицам.</summary>
    public static readonly IReadOnlyList<KeeperScreenBlockInfo> All =
    [
        new(KeeperScreenBlock.Checks, "checks", "Проверки", "fa-bullseye"),
        new(KeeperScreenBlock.Dice, "dice", "Кости", "fa-dice"),
        new(KeeperScreenBlock.Push, "push", "Повтор", "fa-rotate-right"),
        new(KeeperScreenBlock.Luck, "luck", "Удача", "fa-clover"),
        new(KeeperScreenBlock.Damage, "damage", "Урон", "fa-fire"),
        new(KeeperScreenBlock.Firearms, "firearms", "Стрельба", "fa-crosshairs"),
        new(KeeperScreenBlock.Sanity, "sanity", "Рассудок", "fa-brain"),
        new(KeeperScreenBlock.GroupCheck, "group", "Групповая проверка", "fa-users")
    ];

    public static KeeperScreenBlock FromSlug(string? slug) =>
        All.FirstOrDefault(b => string.Equals(b.Slug, slug, StringComparison.OrdinalIgnoreCase))?.Block
        ?? KeeperScreenBlock.Checks;

    public static string Slug(KeeperScreenBlock block) =>
        All.First(b => b.Block == block).Slug;

    /// <summary>Адрес раздела на отдельной странице — для закладки на планшете.</summary>
    public static string Url(KeeperScreenBlock block) => $"/reference?block={Slug(block)}";
}
