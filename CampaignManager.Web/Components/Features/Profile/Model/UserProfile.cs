using CampaignManager.Web.Components.Features.Admin.Model;
using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Web.Components.Features.Profile.Model;

/// <summary>
///     Снимок всего, что личный кабинет показывает о текущем пользователе. Собирается одним
///     вызовом <see cref="Services.ProfileService.GetProfileAsync" />, чтобы страница не делала
///     полдюжины запросов из разметки.
/// </summary>
public sealed class UserProfile
{
    public required string Email { get; init; }

    /// <summary>Отображаемое имя из <c>ApplicationUser.UserName</c>.</summary>
    public required string DisplayName { get; init; }

    public PlayerRole Role { get; init; }

    /// <summary>Кампании, где пользователь игрок или Хранитель.</summary>
    public int CampaignCount { get; init; }

    /// <summary>Неархивные листы сыщиков, принадлежащие пользователю.</summary>
    public int CharacterCount { get; init; }

    /// <summary>
    ///     Сколько строк <c>CampaignPlayer</c> хранят копию имени игрока — столько записей
    ///     затронет синхронизация имени.
    /// </summary>
    public int CampaignPlayerCount { get; init; }

    /// <summary>Последняя заявка на роль Хранителя — <c>null</c>, если пользователь её не подавал.</summary>
    public KeeperApplication? LatestApplication { get; init; }

    public bool IsKeeper => Role is PlayerRole.GameMaster or PlayerRole.Administrator;
}
