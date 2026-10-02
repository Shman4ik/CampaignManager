using CampaignManager.Core.Identity;
using CampaignManager.UI.Shared;

namespace CampaignManager.UI.Identity;

/// <summary>
/// Роли и статусы заявок по-русски — одна копия на кабинет и админку. В v1 роль переводили
/// <c>ToRussianString()</c>, а бейджи красили по именам цветов («yellow», «red»), которых <c>Badge</c> не знал:
/// всё рисовалось одним цветом. Цвета ролей в кабинете и в админке одинаковые.
/// </summary>
public static class RoleLabels
{
    public static string Role(UserRole role) => role switch
    {
        UserRole.Admin => "Администратор",
        UserRole.Keeper => "Хранитель",
        _ => "Игрок",
    };

    public static Tone RoleTone(UserRole role) => role switch
    {
        UserRole.Admin => Tone.Info,
        UserRole.Keeper => Tone.Success,
        _ => Tone.Neutral,
    };

    /// <summary>Что даёт роль — абзац в кабинете.</summary>
    public static string RoleDescription(UserRole role) => role switch
    {
        UserRole.Admin => "Администратор: всё, что может Хранитель, плюс пользователи, роли, заявки и файлы.",
        UserRole.Keeper => "Хранитель: создаёт кампании и сценарии, ведёт журнал, НПС и справочники.",
        _ => "Игрок: участвует в кампаниях и ведёт своих сыщиков.",
    };

    public static string Status(KeeperApplicationStatus status) => status switch
    {
        KeeperApplicationStatus.Approved => "Одобрена",
        KeeperApplicationStatus.Rejected => "Отклонена",
        _ => "Ожидает",
    };

    public static Tone StatusTone(KeeperApplicationStatus status) => status switch
    {
        KeeperApplicationStatus.Approved => Tone.Success,
        KeeperApplicationStatus.Rejected => Tone.Error,
        _ => Tone.Warning,
    };
}
