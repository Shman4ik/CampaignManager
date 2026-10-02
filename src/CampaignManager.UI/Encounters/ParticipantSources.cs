using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Characters;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Encounters;

namespace CampaignManager.UI.Encounters;

/// <summary>Что видит источник: кампания сцены (для её сыщиков) и справочник навыков (лист без него не читается).</summary>
public sealed record ParticipantPickerContext(Guid? CampaignId, SkillCatalog Catalog);

/// <summary>
/// Строка выбора участника: кто, откуда и как из неё получить участников сцены. Лист — один участник (повтор отсечёт
/// ядро), тварь — сколько угодно за раз (<see cref="AllowCount"/>: трое глубоководных одним выбором, знание v1).
/// </summary>
public sealed record ParticipantOption(
    string Key,
    string Name,
    string? Subtitle,
    ParticipantKind Kind,
    Guid? CharacterId,
    bool AllowCount,
    Func<int, CancellationToken, Task<IReadOnlyList<EncounterParticipant>>> CreateAsync);

/// <summary>Строки источника; <see cref="Message"/> — почему пусто (нет кампании), чтобы не гадать.</summary>
public sealed record ParticipantSourceResult(IReadOnlyList<ParticipantOption> Options, string? Message = null);

/// <summary>
/// Источник участников для <see cref="ParticipantPicker"/>. Новый источник — новая реализация в списке страницы, а не
/// вкладка рядом со списком (знание v1: <c>ParticipantPicker</c> — единственный список участников боя и погони).
/// <para>
/// <b>Стык T2.5a:</b> «НПС сценария» — реализация над API состава сценария (<c>scenario_npcs</c>: лист, роль, количество;
/// <c>scenario_creatures</c>: тварь с правкой статблока и количеством). Сторона — <see cref="EncounterParticipants.SideOf"/>
/// по роли, количество больше 1 — пачка с номерами, как у тварей. Страница сцены берёт сценарий из прохождения кампании.
/// </para>
/// </summary>
public interface IParticipantSource
{
    string Key { get; }

    string Label { get; }

    string Icon { get; }

    Task<ParticipantSourceResult> LoadAsync(ParticipantPickerContext context, CancellationToken cancellationToken);
}

/// <summary>Источники, которые есть у любой сцены: сыщики кампании, НПС библиотеки и кампаний, бестиарий.</summary>
public static class ParticipantSources
{
    public static IReadOnlyList<IParticipantSource> Default(ICharactersApi characters, ICatalogApi<CreatureDto> creatures) =>
    [
        new CampaignInvestigatorsSource(characters),
        new NpcLibrarySource(characters),
        new BestiarySource(creatures),
    ];
}

/// <summary>Активные сыщики кампании сцены — с листами (тот же запрос, что групповая проверка ширмы).</summary>
public sealed class CampaignInvestigatorsSource(ICharactersApi characters) : IParticipantSource
{
    public string Key => "investigators";

    public string Label => "Сыщики";

    public string Icon => "fa-user";

    public async Task<ParticipantSourceResult> LoadAsync(ParticipantPickerContext context, CancellationToken cancellationToken)
    {
        if (context.CampaignId is not { } campaignId)
            return new ParticipantSourceResult([], "Сцена вне кампании: сыщиков кампании нет. Листы НПС — во вкладке «НПС».");

        var investigators = await characters.GetCampaignInvestigatorsAsync(campaignId, cancellationToken);
        return new ParticipantSourceResult(
        [
            .. investigators.Select(i => new ParticipantOption(
                $"character:{i.CharacterId}",
                i.Name,
                Subtitle(i),
                ParticipantKind.Investigator,
                i.CharacterId,
                AllowCount: false,
                (_, _) => Task.FromResult<IReadOnlyList<EncounterParticipant>>(
                    [EncounterParticipants.FromSheet(i.CharacterId, CharacterKind.Player, i.Sheet, context.Catalog)]))),
        ], investigators.Count == 0 ? "В кампании нет активных сыщиков." : null);
    }

    private static string Subtitle(InvestigatorDto investigator)
    {
        var occupation = investigator.Sheet.Personal.Occupation;
        return string.Join(" · ", new[] { occupation, investigator.PlayerName }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }
}

/// <summary>НПС из библиотеки и кампаний, которые ведёт Хранитель (как на <c>/npcs</c>). Лист читается при добавлении.</summary>
public sealed class NpcLibrarySource(ICharactersApi characters) : IParticipantSource
{
    public string Key => "npcs";

    public string Label => "НПС";

    public string Icon => "fa-user-secret";

    public async Task<ParticipantSourceResult> LoadAsync(ParticipantPickerContext context, CancellationToken cancellationToken)
    {
        var npcs = await characters.ListAsync(CharacterKind.Npc, archived: false, cancellationToken);
        return new ParticipantSourceResult(
        [
            .. npcs
                // НПС своей кампании — первыми: их и берут в сцену чаще.
                .OrderBy(n => n.CampaignId == context.CampaignId && context.CampaignId is not null ? 0 : 1)
                .ThenBy(n => n.Name, StringComparer.CurrentCulture)
                .Select(n => new ParticipantOption(
                    $"character:{n.Id}",
                    n.Name,
                    string.Join(" · ", new[] { n.Occupation, n.CampaignName ?? "библиотека" }.Where(s => !string.IsNullOrWhiteSpace(s))),
                    ParticipantKind.Npc,
                    n.Id,
                    AllowCount: false,
                    async (_, ct) =>
                    {
                        var character = await characters.GetAsync(n.Id, ct);
                        return [EncounterParticipants.FromSheet(n.Id, CharacterKind.Npc, character.Sheet, context.Catalog)];
                    })),
        ], npcs.Count == 0 ? "В библиотеке нет НПС." : null);
    }
}

/// <summary>Твари бестиария: статблок — снимок чисел, «#N» при нескольких.</summary>
public sealed class BestiarySource(ICatalogApi<CreatureDto> creatures) : IParticipantSource
{
    public string Key => "bestiary";

    public string Label => "Бестиарий";

    public string Icon => "fa-skull";

    public async Task<ParticipantSourceResult> LoadAsync(ParticipantPickerContext context, CancellationToken cancellationToken)
    {
        var list = await creatures.ListAsync(cancellationToken);
        return new ParticipantSourceResult(
        [
            .. list.Items
                .OrderBy(c => c.Name, StringComparer.CurrentCulture)
                .Select(c => new ParticipantOption(
                    $"creature:{c.Id}",
                    c.Name,
                    $"{CatalogText.Of(c.Type)} · ПЗ {c.Statblock.HitPoints}" + (string.IsNullOrWhiteSpace(c.Statblock.SanityLoss) ? "" : $" · рассудок {c.Statblock.SanityLoss}"),
                    ParticipantKind.Creature,
                    CharacterId: null,
                    AllowCount: true,
                    (count, _) => Task.FromResult<IReadOnlyList<EncounterParticipant>>(
                        [.. Enumerable.Range(0, Math.Max(1, count)).Select(_ => EncounterParticipants.FromStatblock(c.Id, c.Name, c.Statblock))]))),
        ], list.Items.Count == 0 ? "Бестиарий пуст." : null);
    }
}
