namespace CampaignManager.UI.Checks;

/// <summary>
/// Итог проверки для того, кто её открыл: ключ цели (<c>char:INT</c>, <c>skill:{id}</c>) и пройдена ли она с учётом Удачи и
/// повтора; <see cref="Passed"/> = <c>null</c> — броска нет или его сбросили («Новая проверка»).
/// </summary>
public sealed record CheckResolution(string? Key, bool? Passed);
