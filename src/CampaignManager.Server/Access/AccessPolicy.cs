using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Characters;
using CampaignManager.Data;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Server.Access;

/// <summary>
/// Все правила доступа 2.0 — здесь, и только здесь (карта прав — AUDIT, «Права»; правила по
/// ресурсам — <c>Server/Access/CLAUDE.md</c>). В v1 те же правила жили в семи сервисах, а четыре
/// справочника не проверяли права вовсе.
/// <para>
/// <c>For…</c> отвечает флагами (<see cref="Access"/>), <c>Can…</c> — разрешением без объекта. Каждый
/// метод записи прикладного сервиса зовёт <c>Demand</c>: не виден объект — 404, виден, но нельзя — 403.
/// Несуществующий объект — тоже <see cref="Access.None"/>, так что «нет» и «не твоё» неразличимы.
/// </para>
/// <para>
/// Администратор может всё, но несуществующий объект и ему отвечает 404. Хранитель — роль платформы
/// (<see cref="SignedInUser.IsKeeper"/>) — ещё не право на чужую кампанию: там решает членство
/// с ролью Keeper в <c>campaign_members</c>.
/// </para>
/// </summary>
public sealed class AccessPolicy(CmDbContext dbContext, CurrentUser currentUser)
{
    // ── Кампания и журнал ───────────────────────────────────────────

    /// <summary>
    /// Кампания и её журнал встреч: Хранитель кампании — всё, игрок-участник — читать, прочие — ничего.
    /// Заметки Хранителя во встречах видит тот, у кого <see cref="Access.CanEdit"/>.
    /// </summary>
    public async Task<Access> ForCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        if (await currentUser.GetAsync(cancellationToken) is not { } user)
        {
            return Access.None;
        }

        if (user.IsAdmin)
        {
            return await dbContext.Campaigns.AnyAsync(c => c.Id == campaignId, cancellationToken) ? Access.Full : Access.None;
        }

        var role = await dbContext.CampaignMembers
            .Where(m => m.CampaignId == campaignId && m.UserId == user.Id)
            .Select(m => (CampaignRole?)m.Role)
            .SingleOrDefaultAsync(cancellationToken);
        return ByMembership(role);
    }

    /// <summary>Завести кампанию — Хранитель по роли (в v1 — кто угодно); создатель становится её Хранителем.</summary>
    public async Task<bool> CanCreateCampaignAsync(CancellationToken cancellationToken = default) =>
        await currentUser.GetAsync(cancellationToken) is { IsKeeper: true };

    /// <summary>Вступить в кампанию: любой вошедший, если кампания не завершена и его в ней ещё нет.</summary>
    public async Task<bool> CanJoinCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        if (await currentUser.GetAsync(cancellationToken) is not { } user)
        {
            return false;
        }

        return await dbContext.Campaigns.AnyAsync(c => c.Id == campaignId
                                                      && c.Status != CampaignStatus.Completed
                                                      && !c.Members.Any(m => m.UserId == user.Id),
            cancellationToken);
    }

    // ── Прохождение и бронь ─────────────────────────────────────────

    /// <summary>Прохождение сценария в кампании: права — как на саму кампанию.</summary>
    public async Task<Access> ForRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        if (await currentUser.GetAsync(cancellationToken) is not { } user)
        {
            return Access.None;
        }

        var run = await dbContext.ScenarioRuns
            .Where(r => r.Id == runId)
            .Select(r => new
            {
                Role = dbContext.CampaignMembers
                    .Where(m => m.CampaignId == r.CampaignId && m.UserId == user.Id)
                    .Select(m => (CampaignRole?)m.Role)
                    .FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (run is null)
        {
            return Access.None;
        }

        return user.IsAdmin ? Access.Full : ByMembership(run.Role);
    }

    /// <summary>Забронировать прегена: любой вошедший, пока у прохождения открыта запись.</summary>
    public async Task<bool> CanReserveAsync(Guid runId, CancellationToken cancellationToken = default) =>
        await currentUser.GetAsync(cancellationToken) is not null
        && await dbContext.ScenarioRuns.AnyAsync(r => r.Id == runId && r.SignupOpen, cancellationToken);

    /// <summary>
    /// Бронь прегена: видят участники кампании, снимают (<see cref="Access.CanDelete"/>) сам игрок или
    /// Хранитель этой кампании. В v1 снять чужую бронь мог любой Хранитель.
    /// </summary>
    public async Task<Access> ForReservationAsync(Guid runId, Guid pregenId, CancellationToken cancellationToken = default)
    {
        if (await currentUser.GetAsync(cancellationToken) is not { } user)
        {
            return Access.None;
        }

        var reservation = await dbContext.RunReservations
            .Where(r => r.RunId == runId && r.PregenId == pregenId)
            .Select(r => new
            {
                r.UserId,
                Role = dbContext.ScenarioRuns
                    .Where(run => run.Id == r.RunId)
                    .SelectMany(run => dbContext.CampaignMembers.Where(m => m.CampaignId == run.CampaignId && m.UserId == user.Id))
                    .Select(m => (CampaignRole?)m.Role)
                    .FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (reservation is null)
        {
            return Access.None;
        }

        if (user.IsAdmin || reservation.UserId == user.Id || reservation.Role is CampaignRole.Keeper)
        {
            return new Access(CanRead: true, CanEdit: false, CanDelete: true);
        }

        return reservation.Role is CampaignRole.Player ? Access.ReadOnly : Access.None;
    }

    // ── Сценарий и раздатка ─────────────────────────────────────────

    /// <summary>
    /// Сценарий — общая библиотека Хранителей: читает и правит любой Хранитель (с локациями, фактами,
    /// тварями, предметами, НПС и раздатками — это правка сценария), удаляет автор или администратор;
    /// сценарий без автора — только администратор. Игроку сценарий не виден (404).
    /// </summary>
    public async Task<Access> ForScenarioAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        if (await currentUser.GetAsync(cancellationToken) is not { IsKeeper: true } user)
        {
            return Access.None;
        }

        var scenario = await dbContext.Scenarios
            .Where(s => s.Id == scenarioId)
            .Select(s => new { s.AuthorId })
            .SingleOrDefaultAsync(cancellationToken);

        if (scenario is null)
        {
            return Access.None;
        }

        return new Access(CanRead: true, CanEdit: true, CanDelete: user.IsAdmin || scenario.AuthorId == user.Id);
    }

    /// <summary>Завести сценарий — Хранитель по роли; он и становится автором.</summary>
    public async Task<bool> CanCreateScenarioAsync(CancellationToken cancellationToken = default) =>
        await currentUser.GetAsync(cancellationToken) is { IsKeeper: true };

    /// <summary>
    /// Раздатка на втором экране: Хранитель — как сценарий; игрок видит раздатку, если он участник
    /// кампании, где этот сценарий проходят (есть прохождение). Пометку Хранителя (<c>keeper_note</c>)
    /// отдают только тому, у кого <see cref="Access.CanEdit"/>.
    /// </summary>
    public async Task<Access> ForHandoutAsync(Guid handoutId, CancellationToken cancellationToken = default)
    {
        if (await currentUser.GetAsync(cancellationToken) is not { } user)
        {
            return Access.None;
        }

        var handout = await dbContext.ScenarioHandouts
            .Where(h => h.Id == handoutId)
            .Select(h => new
            {
                PlayedByMe = dbContext.ScenarioRuns.Any(r => r.ScenarioId == h.ScenarioId
                    && dbContext.CampaignMembers.Any(m => m.CampaignId == r.CampaignId && m.UserId == user.Id)),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (handout is null)
        {
            return Access.None;
        }

        if (user.IsKeeper)
        {
            return Access.Full;
        }

        return handout.PlayedByMe ? Access.ReadOnly : Access.None;
    }

    // ── Лист ────────────────────────────────────────────────────────

    /// <summary>
    /// Лист по виду:
    /// <list type="bullet">
    /// <item>сыщик игрока — владелец и Хранитель его кампании; остальные, включая соседей по столу, — ничего;</item>
    /// <item>преген — читает любой вошедший (бронь на ваншот), правит любой Хранитель (как сценарий);</item>
    /// <item>НПС кампании — её Хранитель; НПС из библиотеки — любой Хранитель; игроку НПС не виден.</item>
    /// </list>
    /// </summary>
    public async Task<Access> ForCharacterAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        if (await currentUser.GetAsync(cancellationToken) is not { } user)
        {
            return Access.None;
        }

        var character = await dbContext.Characters
            .Where(c => c.Id == characterId)
            .Select(c => new
            {
                c.Kind,
                c.OwnerId,
                c.CampaignId,
                KeepsCampaign = c.CampaignId != null && dbContext.CampaignMembers.Any(m =>
                    m.CampaignId == c.CampaignId && m.UserId == user.Id && m.Role == CampaignRole.Keeper),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (character is null)
        {
            return Access.None;
        }

        if (user.IsAdmin)
        {
            return Access.Full;
        }

        return character.Kind switch
        {
            CharacterKind.Player => character.OwnerId == user.Id || character.KeepsCampaign ? Access.Full : Access.None,
            CharacterKind.Pregen => user.IsKeeper ? Access.Full : Access.ReadOnly,
            CharacterKind.Npc when character.CampaignId is null => user.IsKeeper ? Access.Full : Access.None,
            CharacterKind.Npc => character.KeepsCampaign ? Access.Full : Access.None,
            _ => Access.None,
        };
    }

    /// <summary>
    /// Завести лист: своего сыщика — любой (в кампанию — только её участник); прегена — Хранитель;
    /// НПС в кампанию — её Хранитель, в библиотеку — любой Хранитель. В v1 прегена и НПС в чужой
    /// сценарий или кампанию клал любой Хранитель.
    /// </summary>
    public async Task<bool> CanCreateCharacterAsync(CharacterKind kind, Guid? campaignId, CancellationToken cancellationToken = default)
    {
        if (await currentUser.GetAsync(cancellationToken) is not { } user)
        {
            return false;
        }

        if (user.IsAdmin)
        {
            return true;
        }

        return kind switch
        {
            CharacterKind.Player => campaignId is not { } id
                                    || await dbContext.CampaignMembers.AnyAsync(m => m.CampaignId == id && m.UserId == user.Id, cancellationToken),
            CharacterKind.Pregen => user.IsKeeper,
            CharacterKind.Npc => campaignId is not { } id ? user.IsKeeper : await KeepsCampaignAsync(id, user.Id, cancellationToken),
            _ => false,
        };
    }

    // ── Справочники и фонотека ──────────────────────────────────────

    /// <summary>
    /// Справочники (навыки, профессии, оружие, заклинания, книги, предметы, бестиарий) и фонотека:
    /// читает любой вошедший, заводит, правит и удаляет Хранитель. Существование записи проверяет сервис.
    /// </summary>
    public async Task<Access> ForCatalogAsync(CancellationToken cancellationToken = default) =>
        await currentUser.GetAsync(cancellationToken) switch
        {
            null => Access.None,
            { IsKeeper: true } => Access.Full,
            _ => Access.ReadOnly,
        };

    // ── Файлы ───────────────────────────────────────────────────────

    /// <summary>
    /// Файлы (<c>cm.files</c>): читать и загружать — любой вошедший (портреты, раздатки, треки);
    /// права на то, где файл показан, — у владельца ссылки (лист, сценарий). Удалять сирот —
    /// администратор (<see cref="Access.CanDelete"/>).
    /// </summary>
    public async Task<Access> ForFilesAsync(CancellationToken cancellationToken = default) =>
        await currentUser.GetAsync(cancellationToken) switch
        {
            null => Access.None,
            { IsAdmin: true } => Access.Full,
            _ => new Access(CanRead: true, CanEdit: true, CanDelete: false),
        };

    // ── Сцена ───────────────────────────────────────────────────────

    /// <summary>Бой или погоня — только Хранитель, который их ведёт.</summary>
    public async Task<Access> ForEncounterAsync(Guid encounterId, CancellationToken cancellationToken = default)
    {
        if (await currentUser.GetAsync(cancellationToken) is not { } user)
        {
            return Access.None;
        }

        var keeperId = await dbContext.Encounters
            .Where(e => e.Id == encounterId)
            .Select(e => (Guid?)e.KeeperId)
            .SingleOrDefaultAsync(cancellationToken);

        return keeperId is not null && (user.IsAdmin || keeperId == user.Id) ? Access.Full : Access.None;
    }

    /// <summary>Начать сцену — Хранитель; в кампании — только её Хранитель.</summary>
    public async Task<bool> CanStartEncounterAsync(Guid? campaignId, CancellationToken cancellationToken = default)
    {
        if (await currentUser.GetAsync(cancellationToken) is not { IsKeeper: true } user)
        {
            return false;
        }

        return user.IsAdmin || campaignId is not { } id || await KeepsCampaignAsync(id, user.Id, cancellationToken);
    }

    // ── Админка ─────────────────────────────────────────────────────

    /// <summary>Пользователи, роли, заявки в Хранители — администратор.</summary>
    public async Task<bool> CanAdministerAsync(CancellationToken cancellationToken = default) =>
        await currentUser.GetAsync(cancellationToken) is { IsAdmin: true };

    private static Access ByMembership(CampaignRole? role) => role switch
    {
        CampaignRole.Keeper => Access.Full,
        CampaignRole.Player => Access.ReadOnly,
        _ => Access.None,
    };

    private Task<bool> KeepsCampaignAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.CampaignMembers.AnyAsync(m => m.CampaignId == campaignId && m.UserId == userId && m.Role == CampaignRole.Keeper,
            cancellationToken);
}
