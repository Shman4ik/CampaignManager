using System.Text.Json.Serialization;
using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Web.Components.Features.Campaigns.Models;

/// <summary>
///     Всё, что главная показывает о кампаниях, одним снимком: его собирает
///     <c>CampaignService.GetHomeCampaignsAsync</c> и раздаёт три блока главной.
///     <c>UserEmail</c> — почта вошедшего (<c>null</c> — аноним), <c>IsKeeper</c> — роль Хранителя
///     или администратора из claims, <c>Mine</c> — кампании, где пользователь состоит игроком (новые
///     сверху), <c>Available</c> — незавершённые кампании, куда он ещё не вступил.
///     <para>
///         Плоский и без EF-навигаций намеренно: снимок переезжает из пререндера в интерактивный
///         рендер через <c>[PersistentState]</c> (JSON), а граф <c>Campaign → Players → Characters</c>
///         циклический. Полных листов тут тоже нет — главной нужны имя, профессия и статус.
///     </para>
/// </summary>
public sealed record HomeCampaigns(
    string? UserEmail,
    bool IsKeeper,
    List<HomeCampaign> Mine,
    List<HomeAvailableCampaign> Available)
{
    [JsonIgnore]
    public bool IsAuthenticated => UserEmail is not null;
}

/// <summary>
///     Кампания пользователя на главной. <c>KeptByMe</c> — пользователь Хранитель именно этой
///     кампании; <c>MyCharacter</c> — его активный лист в ней. <c>Players</c> (игроки со всеми
///     листами) и <c>Npcs</c> (НПС кампании — листы с <c>CampaignId</c>, а не слоты игроков)
///     заполнены только у кампаний, которые пользователь ведёт; у чужих они пусты.
/// </summary>
public sealed record HomeCampaign(
    Guid Id,
    string Name,
    CampaignStatus Status,
    string? KeeperEmail,
    bool KeptByMe,
    int PlayerCount,
    HomeCharacter? MyCharacter,
    List<HomePlayer> Players,
    List<HomeCharacter> Npcs);

/// <summary>Игрок кампании и его листы.</summary>
public sealed record HomePlayer(string PlayerName, List<HomeCharacter> Characters);

/// <summary>Строка листа на главной. Профессия у НПС кампании не читается и пуста.</summary>
public sealed record HomeCharacter(
    Guid Id,
    string Name,
    string? Occupation,
    CharacterKind Kind,
    CharacterStatus Status);

/// <summary>Кампания, в которую можно вступить.</summary>
public sealed record HomeAvailableCampaign(Guid Id, string Name, DateTimeOffset CreatedAt, string? KeeperEmail);
