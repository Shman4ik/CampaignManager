using CampaignManager.Contracts.Campaigns;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Characters;
using CampaignManager.Data;
using CampaignManager.Server.Access;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Server.Campaigns;

/// <summary>
/// Главная одним запросом (<c>GET /api/v1/home</c>): мои кампании, доступные для вступления, открытые на
/// запись ваншоты и НПС кампаний, которые я веду. В v1 три блока главной читали каждый своё, а аноним
/// получал список кампаний с почтами Хранителей; здесь эндпоинт закрыт входом, почт нет нигде.
/// <para>
/// Права: свои кампании — по членству (Хранитель видит и игроков с их листами, и НПС, игрок — только свой
/// активный лист), доступные и ваншоты — то, что любой вошедший может сделать сам (вступить,
/// забронировать). Правила те же, что у <see cref="AccessPolicy"/>; здесь они только фильтры запросов.
/// </para>
/// </summary>
public sealed class HomeService(CmDbContext dbContext, CurrentUser currentUser)
{
    private const string Unnamed = "Без имени";

    public async Task<HomeDto> GetAsync(CancellationToken cancellationToken)
    {
        var user = await currentUser.GetAsync(cancellationToken) ?? throw AccessDeniedException.NotFound();
        var me = user.Id;

        var mine = await dbContext.Campaigns
            .Where(c => c.Members.Any(m => m.UserId == me))
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Kind,
                c.Status,
                MyRole = c.Members.Where(m => m.UserId == me).Select(m => m.Role).First(),
                Keeper = c.Members.Where(m => m.Role == CampaignRole.Keeper)
                    .Select(m => new { m.DisplayName, UserName = dbContext.Users.Where(u => u.Id == m.UserId).Select(u => u.DisplayName).First() })
                    .FirstOrDefault(),
                PlayerCount = c.Members.Count(m => m.Role == CampaignRole.Player),
            })
            .ToListAsync(cancellationToken);

        var mineIds = mine.Select(c => c.Id).ToList();
        var keptIds = mine.Where(c => c.MyRole == CampaignRole.Keeper).Select(c => c.Id).ToList();

        // Игроки кампаний, которые я веду: строка «Игрок — его листы», в том числе у тех, кто листа не завёл.
        var players = await dbContext.CampaignMembers
            .Where(m => keptIds.Contains(m.CampaignId) && m.Role == CampaignRole.Player)
            .OrderBy(m => m.JoinedAt)
            .Select(m => new
            {
                m.CampaignId,
                m.UserId,
                m.DisplayName,
                UserName = dbContext.Users.Where(u => u.Id == m.UserId).Select(u => u.DisplayName).First(),
            })
            .ToListAsync(cancellationToken);

        // Листы — без документа: имя и профессия — generated-колонки. Хранителю — все листы игроков и НПС
        // его кампаний, игроку — только свой активный лист.
        var characters = await dbContext.Characters
            .Where(ch => ch.CampaignId != null && mineIds.Contains(ch.CampaignId.Value)
                         && ((ch.Kind == CharacterKind.Player
                              && (keptIds.Contains(ch.CampaignId.Value) || (ch.OwnerId == me && ch.Status == CharacterStatus.Active)))
                             || (ch.Kind == CharacterKind.Npc && keptIds.Contains(ch.CampaignId.Value))))
            .OrderBy(ch => ch.CreatedAt)
            .Select(ch => new { ch.Id, ch.Name, ch.Occupation, ch.Kind, ch.Status, ch.OwnerId, CampaignId = ch.CampaignId!.Value })
            .ToListAsync(cancellationToken);

        var available = await dbContext.Campaigns
            .Where(c => c.Status != CampaignStatus.Completed
                        && c.Kind != CampaignKind.OneShot
                        && !c.Members.Any(m => m.UserId == me))
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Status,
                c.CreatedAt,
                Keeper = c.Members.Where(m => m.Role == CampaignRole.Keeper)
                    .Select(m => new { m.DisplayName, UserName = dbContext.Users.Where(u => u.Id == m.UserId).Select(u => u.DisplayName).First() })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return new HomeDto(
            [
                .. mine.Select(c =>
                {
                    var kept = c.MyRole == CampaignRole.Keeper;
                    var sheets = characters.Where(ch => ch.CampaignId == c.Id).ToList();
                    var myCharacter = sheets.FirstOrDefault(ch =>
                        ch.Kind == CharacterKind.Player && ch.OwnerId == me && ch.Status == CharacterStatus.Active);

                    return new HomeCampaignDto(
                        c.Id,
                        c.Name,
                        c.Kind,
                        c.Status,
                        c.MyRole,
                        kept ? null : PublicNames.Of(c.Keeper?.DisplayName, c.Keeper?.UserName),
                        c.PlayerCount,
                        myCharacter is null ? null : Character(myCharacter.Id, myCharacter.Name, myCharacter.Occupation, myCharacter.Kind, myCharacter.Status),
                        kept
                            ?
                            [
                                .. players.Where(p => p.CampaignId == c.Id).Select(p => new HomePlayerDto(
                                    p.UserId,
                                    PublicNames.Of(p.DisplayName, p.UserName),
                                    [
                                        .. sheets.Where(ch => ch.Kind == CharacterKind.Player && ch.OwnerId == p.UserId)
                                            .Select(ch => Character(ch.Id, ch.Name, ch.Occupation, ch.Kind, ch.Status)),
                                    ])),
                            ]
                            : [],
                        kept
                            ? [.. sheets.Where(ch => ch.Kind == CharacterKind.Npc).Select(ch => Character(ch.Id, ch.Name, ch.Occupation, ch.Kind, ch.Status))]
                            : []);
                }),
            ],
            [.. available.Select(c => new HomeAvailableCampaignDto(c.Id, c.Name, c.Status, c.CreatedAt,
                PublicNames.Of(c.Keeper?.DisplayName, c.Keeper?.UserName)))],
            await OneShotsAsync(me, cancellationToken));
    }

    /// <summary>
    /// Прохождения с открытой записью — их видит любой вошедший: забронировать прегена может каждый
    /// (<see cref="AccessPolicy.CanReserveAsync"/>). Прегены — листы сценария прохождения, кроме архивных;
    /// бронь — строка <c>run_reservations</c>, кто занял — по псевдониму в кампании или имени.
    /// </summary>
    private async Task<IReadOnlyList<HomeOneShotDto>> OneShotsAsync(Guid me, CancellationToken cancellationToken)
    {
        var runs = await dbContext.ScenarioRuns
            .Where(r => r.SignupOpen)
            .OrderBy(r => r.ScheduledAt == null)
            .ThenBy(r => r.ScheduledAt)
            .ThenBy(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.CampaignId,
                r.ScenarioId,
                ScenarioName = dbContext.Scenarios.Where(s => s.Id == r.ScenarioId).Select(s => s.Name).First(),
                r.ScheduledAt,
                r.Announcement,
                Keeper = dbContext.CampaignMembers.Where(m => m.CampaignId == r.CampaignId && m.Role == CampaignRole.Keeper)
                    .Select(m => new
                    {
                        m.UserId,
                        m.DisplayName,
                        UserName = dbContext.Users.Where(u => u.Id == m.UserId).Select(u => u.DisplayName).First(),
                    })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        if (runs.Count == 0)
        {
            return [];
        }

        var scenarioIds = runs.Select(r => r.ScenarioId).Distinct().ToList();
        var pregens = await dbContext.Characters
            .Where(ch => ch.Kind == CharacterKind.Pregen && ch.ScenarioId != null && scenarioIds.Contains(ch.ScenarioId.Value)
                         && ch.Status != CharacterStatus.Archived)
            .OrderBy(ch => ch.Name)
            .Select(ch => new { ch.Id, ch.Name, ch.Occupation, ScenarioId = ch.ScenarioId!.Value })
            .ToListAsync(cancellationToken);

        var runIds = runs.Select(r => r.Id).ToList();
        var reservations = await dbContext.RunReservations
            .Where(rr => runIds.Contains(rr.RunId))
            .Select(rr => new
            {
                rr.RunId,
                rr.PregenId,
                rr.UserId,
                Alias = dbContext.ScenarioRuns.Where(r => r.Id == rr.RunId)
                    .SelectMany(r => dbContext.CampaignMembers.Where(m => m.CampaignId == r.CampaignId && m.UserId == rr.UserId))
                    .Select(m => m.DisplayName)
                    .FirstOrDefault(),
                UserName = dbContext.Users.Where(u => u.Id == rr.UserId).Select(u => u.DisplayName).First(),
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. runs.Select(r => new HomeOneShotDto(
                r.Id,
                r.CampaignId,
                r.ScenarioId,
                r.ScenarioName,
                r.ScheduledAt,
                r.Announcement,
                PublicNames.Of(r.Keeper?.DisplayName, r.Keeper?.UserName),
                r.Keeper?.UserId == me,
                [
                    .. pregens.Where(p => p.ScenarioId == r.ScenarioId).Select(p =>
                    {
                        var reservation = reservations.FirstOrDefault(rr => rr.RunId == r.Id && rr.PregenId == p.Id);
                        return new HomePregenDto(
                            p.Id,
                            p.Name ?? Unnamed,
                            p.Occupation,
                            reservation is not null,
                            reservation is null ? null : PublicNames.Of(reservation.Alias, reservation.UserName),
                            reservation?.UserId == me);
                    }),
                ])),
        ];
    }

    private static HomeCharacterDto Character(Guid id, string? name, string? occupation, CharacterKind kind, CharacterStatus status) =>
        new(id, name ?? Unnamed, string.IsNullOrWhiteSpace(occupation) ? null : occupation, kind, status);
}
