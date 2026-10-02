using CampaignManager.Core;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Characters;
using CampaignManager.UI.Shared;

namespace CampaignManager.UI.Campaigns;

/// <summary>
/// Подписи и тона кампаний — в одном месте. В v1 статусы были переведены дважды и по-разному
/// («На паузе» в списке, «Пауза» на главной), и тона бейджей тоже расходились.
/// </summary>
public static class CampaignLabels
{
    public static string Status(CampaignStatus status) => status switch
    {
        CampaignStatus.Planning => "Планирование",
        CampaignStatus.Active => "Активна",
        CampaignStatus.OnHold => "На паузе",
        CampaignStatus.Completed => "Завершена",
        _ => status.ToString(),
    };

    public static Tone StatusTone(CampaignStatus status) => status switch
    {
        CampaignStatus.Active => Tone.Success,
        CampaignStatus.OnHold => Tone.Warning,
        CampaignStatus.Completed => Tone.Neutral,
        _ => Tone.Info,
    };

    public static string Kind(CampaignKind kind) => kind switch
    {
        CampaignKind.OneShot => "Ваншот",
        _ => "Кампания",
    };

    public static string Era(Era era) => era switch
    {
        Core.Era.Modern => "Современность",
        _ => "1920-е (классика)",
    };

    public static string Role(CampaignRole role) => role switch
    {
        CampaignRole.Keeper => "Хранитель",
        _ => "Игрок",
    };

    /// <summary>Статус листа на главной и в списке игроков кампании.</summary>
    public static string CharacterStatus(CharacterStatus status) => status switch
    {
        Core.Characters.CharacterStatus.Active => "Активен",
        Core.Characters.CharacterStatus.Inactive => "Неактивен",
        Core.Characters.CharacterStatus.Retired => "Выбыл",
        Core.Characters.CharacterStatus.Archived => "В архиве",
        _ => status.ToString(),
    };

    public static Tone CharacterStatusTone(CharacterStatus status) => status switch
    {
        Core.Characters.CharacterStatus.Active => Tone.Success,
        Core.Characters.CharacterStatus.Inactive => Tone.Warning,
        _ => Tone.Neutral,
    };

    /// <summary>Человек без показываемого имени (имени нет или оно — почта, а почты не показываем).</summary>
    public const string Unnamed = "Без имени";

    public static string Person(string? name) => string.IsNullOrWhiteSpace(name) ? Unnamed : name;
}
