using CampaignManager.Contracts.Campaigns;

namespace CampaignManager.UI.Campaigns;

/// <summary>
/// Куда главная кладёт разовую игру с открытой записью — по тому, кто смотрит (владелец 2026-10-04):
/// <list type="bullet">
/// <item>ведущий — в карточку своей кампании (строка «Запись открыта»), анонса для него нет: это приглашение игрокам;</item>
/// <item>игрок, у которого в этой игре уже есть сыщик (занятое место или свой лист), — тоже в карточку кампании;</item>
/// <item>остальным — анонс (<see cref="Invites"/>), а сама кампания игры уходит из «Можно вступить»: анонс вступает сам.</item>
/// </list>
/// </summary>
public sealed class HomeLayout
{
    private readonly IReadOnlyDictionary<Guid, HomeOneShotDto> _runByCampaign;
    private readonly HashSet<Guid> _memberOf;

    private HomeLayout(HomeDto home)
    {
        _runByCampaign = home.OneShots.GroupBy(r => r.CampaignId).ToDictionary(g => g.Key, g => g.First());
        _memberOf = [.. home.Mine.Select(c => c.Id)];
        var seated = home.Mine.Where(c => c.MyCharacter is not null).Select(c => c.Id).ToHashSet();

        Invites = [.. home.OneShots.Where(r => !r.IsMine && !r.Pregens.Any(p => p.IsMine) && !seated.Contains(r.CampaignId))];
        var invited = Invites.Select(r => r.CampaignId).ToHashSet();
        Available = [.. home.Available.Where(c => !invited.Contains(c.Id))];
    }

    /// <summary>Анонсы, в которые можно записаться, — во всю колонку главной.</summary>
    public IReadOnlyList<HomeOneShotDto> Invites { get; }

    /// <summary>«Можно вступить» без кампаний, чей анонс уже на странице.</summary>
    public IReadOnlyList<HomeAvailableCampaignDto> Available { get; }

    public static HomeLayout Of(HomeDto home) => new(home);

    /// <summary>Разовая игра кампании с открытой записью — для карточки кампании (ведущему и записавшемуся).</summary>
    public HomeOneShotDto? RunOf(Guid campaignId) => _runByCampaign.GetValueOrDefault(campaignId);

    public bool IsMember(Guid campaignId) => _memberOf.Contains(campaignId);
}
