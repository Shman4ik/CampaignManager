using CampaignManager.Web.Components.Features.Campaigns.Models;
using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Web.Components.Features.Checks.Model;

/// <summary>
///     Сыщик, за которого бросают проверку вне его листа (режим игры сценария, групповая проверка).
///     Лист здесь только для чтения: запись в него идёт через автосохранение открытого листа,
///     а не отсюда — иначе открытый у игрока лист затёр бы отметку своим следующим сохранением.
/// </summary>
public sealed record CheckInvestigator(Guid Id, string Name, string PlayerName, Character Character)
{
    /// <summary>
    ///     Активные листы игроков кампании — тот же состав, что «Персонажи кампании» в
    ///     <c>Combat/Components/ParticipantPicker</c>. Забронированный преген ваншота тоже здесь:
    ///     бронь переводит его в кампанию сценария.
    /// </summary>
    public static List<CheckInvestigator> FromCampaign(Campaign? campaign) =>
        campaign is null
            ? []
            : campaign.Players
                .SelectMany(player => player.Characters
                    .Where(c => c.Status == CharacterStatus.Active)
                    .Select(c => new CheckInvestigator(
                        c.Id,
                        string.IsNullOrWhiteSpace(c.Character.PersonalInfo.Name)
                            ? c.CharacterName
                            : c.Character.PersonalInfo.Name,
                        player.PlayerName,
                        c.Character)))
                .OrderBy(i => i.Name, StringComparer.CurrentCulture)
                .ToList();
}
