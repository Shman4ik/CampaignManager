using CampaignManager.Core;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Characters;

namespace CampaignManager.Contracts.Campaigns;

// Почт здесь нет нигде: человек показывается по псевдониму в кампании или отображаемому имени, а имя,
// похожее на почту (так его заводит вход без name у провайдера), приходит как null.

/// <summary>Кампания в списке.</summary>
/// <param name="MyRole">Моя роль в кампании; <c>null</c> — я не участник (администратор видит и чужие).</param>
/// <param name="KeeperName">Хранитель по псевдониму или имени; <c>null</c> — имени нет или оно похоже на почту.</param>
/// <param name="PlayerCount">Участников с ролью игрока; Хранитель не считается.</param>
public sealed record CampaignSummaryDto(
    Guid Id,
    string Name,
    CampaignKind Kind,
    CampaignStatus Status,
    Era Era,
    DateTimeOffset CreatedAt,
    CampaignRole? MyRole,
    string? KeeperName,
    int PlayerCount,
    bool CanEdit,
    bool CanDelete);

/// <summary>Кампания с участниками.</summary>
/// <param name="CanLeave">Я игрок этой кампании и могу выйти. Хранитель выйти не может — кампания без него не живёт.</param>
public sealed record CampaignDetailsDto(
    CampaignSummaryDto Campaign,
    IReadOnlyList<CampaignMemberDto> Members,
    bool CanLeave);

/// <param name="Name">Как участника видят в этой кампании: псевдоним, иначе имя; <c>null</c> — показать нечего.</param>
/// <param name="Alias">Псевдоним в этой кампании; <c>null</c> — используется имя из профиля.</param>
/// <param name="CanRename">Мне можно сменить его псевдоним: свой — всегда, чужой — Хранителю кампании.</param>
/// <param name="CanRemove">Мне можно исключить его: игрока — Хранителю кампании.</param>
public sealed record CampaignMemberDto(
    Guid UserId,
    string? Name,
    string? Alias,
    CampaignRole Role,
    DateTimeOffset JoinedAt,
    bool IsMe,
    bool CanRename,
    bool CanRemove);

/// <summary>Одна форма на создание и правку.</summary>
public sealed record CampaignInput(string Name, CampaignKind Kind, CampaignStatus Status, Era Era);

/// <param name="DisplayName">Как меня звать в этой кампании; пусто или совпадает с именем профиля — псевдонима нет.</param>
public sealed record JoinCampaignRequest(string? DisplayName);

/// <param name="DisplayName">Новый псевдоним; пусто — вернуть имя из профиля.</param>
public sealed record UpdateMemberRequest(string? DisplayName);

// ── Журнал ──────────────────────────────────────────────────────────

/// <summary>
/// Журнал встреч в том виде, в каком его можно показать мне. <see cref="CampaignSessionDto.KeeperNotes"/>
/// у читателя без права правки всегда <c>null</c> — заметки вырезает сервер.
/// </summary>
/// <param name="NextNumber">Номер, который предложить новой встрече.</param>
/// <param name="Runs">Прохождения кампании, к которым можно привязать встречу (только тому, кто правит).</param>
/// <param name="Investigators">Активные листы игроков для подсказки о фазе развития: Хранителю — все, игроку — свой.</param>
public sealed record CampaignJournalDto(
    Guid CampaignId,
    string CampaignName,
    bool CanEdit,
    int NextNumber,
    IReadOnlyList<CampaignSessionDto> Sessions,
    IReadOnlyList<JournalRunOption> Runs,
    IReadOnlyList<JournalInvestigator> Investigators);

/// <param name="ScenarioName">Сценарий прохождения, к которому привязана встреча.</param>
public sealed record CampaignSessionDto(
    Guid Id,
    int Number,
    DateOnly SessionDate,
    string? Title,
    string? Summary,
    string? KeeperNotes,
    Guid? RunId,
    Guid? ScenarioId,
    string? ScenarioName,
    bool ScenarioCompleted);

/// <summary>Прохождение сценария в кампании — вариант в форме встречи.</summary>
public sealed record JournalRunOption(Guid RunId, Guid ScenarioId, string ScenarioName, DateTimeOffset? ScheduledAt);

/// <param name="PlayerName">Игрок по псевдониму или имени; <c>null</c> — имени нет.</param>
public sealed record JournalInvestigator(Guid CharacterId, string Name, string? PlayerName);

/// <summary>Встреча из формы — и новая, и правка.</summary>
public sealed record CampaignSessionInput(
    DateOnly SessionDate,
    int Number,
    string? Title,
    string? Summary,
    string? KeeperNotes,
    Guid? RunId,
    bool ScenarioCompleted);

// ── Главная ─────────────────────────────────────────────────────────

/// <summary>Всё о кампаниях, что показывает главная, — одним запросом.</summary>
/// <param name="Mine">Кампании, где я участник (Хранитель или игрок), новые сверху.</param>
/// <param name="Available">Незавершённые кампании и ваншоты, куда я ещё не вступил.</param>
/// <param name="OneShots">Прохождения с открытой записью на прегенов.</param>
public sealed record HomeDto(
    IReadOnlyList<HomeCampaignDto> Mine,
    IReadOnlyList<HomeAvailableCampaignDto> Available,
    IReadOnlyList<HomeOneShotDto> OneShots);

/// <param name="KeeperName">Хранитель чужой кампании; у своей — <c>null</c> (об этом говорит роль).</param>
/// <param name="MyCharacter">Мой активный лист в кампании.</param>
/// <param name="Players">Игроки со всеми листами — только в кампаниях, которые я веду.</param>
/// <param name="Npcs">НПС кампании — только в кампаниях, которые я веду.</param>
public sealed record HomeCampaignDto(
    Guid Id,
    string Name,
    CampaignKind Kind,
    CampaignStatus Status,
    CampaignRole MyRole,
    string? KeeperName,
    int PlayerCount,
    HomeCharacterDto? MyCharacter,
    IReadOnlyList<HomePlayerDto> Players,
    IReadOnlyList<HomeCharacterDto> Npcs);

public sealed record HomePlayerDto(Guid UserId, string? Name, IReadOnlyList<HomeCharacterDto> Characters);

public sealed record HomeCharacterDto(Guid Id, string Name, string? Occupation, CharacterKind Kind, CharacterStatus Status);

public sealed record HomeAvailableCampaignDto(Guid Id, string Name, CampaignKind Kind, CampaignStatus Status, DateTimeOffset CreatedAt, string? KeeperName);

/// <param name="IsMine">Я веду это прохождение (Хранитель его кампании).</param>
public sealed record HomeOneShotDto(
    Guid RunId,
    Guid CampaignId,
    Guid ScenarioId,
    string ScenarioName,
    DateTimeOffset? ScheduledAt,
    string? Announcement,
    string? KeeperName,
    bool IsMine,
    IReadOnlyList<HomePregenDto> Pregens);

/// <param name="ReservedBy">Кто забронировал (по псевдониму или имени); <c>null</c> — свободен или имени нет.</param>
public sealed record HomePregenDto(Guid Id, string Name, string? Occupation, bool IsReserved, string? ReservedBy, bool IsMine);
