using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Encounters;
using CampaignManager.UI.Shared;

namespace CampaignManager.UI.Encounters;

/// <summary>
/// Что видит источник: кампания сцены (для её сыщиков), справочник навыков (лист без него не читается) и сценарий,
/// выбранный в окне (для состава сценария).
/// </summary>
public sealed record ParticipantPickerContext(Guid? CampaignId, SkillCatalog Catalog, Guid? ScenarioId = null);

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
    Func<int, CancellationToken, Task<IReadOnlyList<EncounterParticipant>>> CreateAsync)
{
    /// <summary>Сколько предложить сразу: количество из состава сценария.</summary>
    public int DefaultCount { get; init; } = 1;

    /// <summary>Тип для отбора кнопками над списком (бестиарий: «Монстры Мифов», «Животные»); null — отбора нет.</summary>
    public string? Category { get; init; }
}

/// <summary>Строки источника; <see cref="Message"/> — почему пусто (нет кампании), чтобы не гадать.</summary>
public sealed record ParticipantSourceResult(IReadOnlyList<ParticipantOption> Options, string? Message = null);

/// <summary>
/// Источник участников для <see cref="ParticipantPicker"/>. Новый источник — новая реализация в списке страницы, а не
/// вкладка рядом со списком (знание v1: <c>ParticipantPicker</c> — единственный список участников боя и погони).
/// </summary>
public interface IParticipantSource
{
    string Key { get; }

    string Label { get; }

    string Icon { get; }

    /// <summary>Источнику нужен сценарий — окно показывает выбор сценария (<see cref="ParticipantPickerContext.ScenarioId"/>).</summary>
    bool UsesScenario => false;

    Task<ParticipantSourceResult> LoadAsync(ParticipantPickerContext context, CancellationToken cancellationToken);
}

/// <summary>Источники, которые есть у любой сцены: сыщики кампании, НПС библиотеки и кампаний, бестиарий.</summary>
public static class ParticipantSources
{
    public static IReadOnlyList<IParticipantSource> Default(ICharactersApi characters, ICatalogApi<CreatureDto> creatures, IScenariosApi scenarios) =>
    [
        new CampaignInvestigatorsSource(characters),
        new ScenarioCastSource(scenarios, characters),
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
        // Состояние — в строке выбора: погибшего сыщика не добавляют вслепую (в погоне он блокировал начало).
        var hitPoints = investigator.Sheet.Current.HitPoints;
        var condition = investigator.Sheet.Condition;
        var state = condition.Dead ? "ПЗ 0 · мёртв"
            : condition.Dying ? "ПЗ 0 · при смерти"
            : hitPoints <= 0 ? "ПЗ 0"
            : condition.Unconscious ? $"ПЗ {hitPoints} · без сознания"
            : condition.MajorWound ? $"ПЗ {hitPoints} · серьёзная рана" : null;
        return string.Join(" · ", new[] { occupation, investigator.PlayerName, state }.Where(s => !string.IsNullOrWhiteSpace(s)));
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
                    // Тип — кнопками над списком, в строке его не повторяем.
                    $"ПЗ {c.Statblock.HitPoints}" + (string.IsNullOrWhiteSpace(c.Statblock.SanityLoss) ? "" : $" · рассудок {DiceText.Format(c.Statblock.SanityLoss)}"),
                    ParticipantKind.Creature,
                    CharacterId: null,
                    AllowCount: true,
                    (count, _) => Task.FromResult<IReadOnlyList<EncounterParticipant>>(
                        [.. Enumerable.Range(0, Math.Max(1, count)).Select(_ => EncounterParticipants.FromStatblock(c.Id, c.Name, c.Statblock))]))
                {
                    Category = CatalogText.Of(c.Type),
                }),
        ], list.Items.Count == 0 ? "Бестиарий пуст." : null);
    }
}

/// <summary>
/// Состав сценария (T2.5a): НПС — листы со стороной по роли (<see cref="EncounterParticipants.SideOf"/>: союзник — с сыщиками,
/// враг — против), твари — итоговый статблок сценария (своя версия поверх бестиария), количество — из состава.
/// <para>
/// <b>Пачка НПС</b> («трое громил» с одного листа, знание v1): при количестве больше одного все участники — <b>статисты</b>:
/// снимок листа без ссылки на него, с номерами «#1, #2». Урон статиста в общий лист не пишется — лист у троих один, и
/// «один лист — один участник» соблюдается. Один участник — обычная ссылка на лист.
/// </para>
/// </summary>
public sealed class ScenarioCastSource(IScenariosApi scenarios, ICharactersApi characters) : IParticipantSource
{
    public string Key => "scenario";

    public string Label => "Сценарий";

    public string Icon => "fa-masks-theater";

    public bool UsesScenario => true;

    public async Task<ParticipantSourceResult> LoadAsync(ParticipantPickerContext context, CancellationToken cancellationToken)
    {
        if (context.ScenarioId is not { } scenarioId)
            return new ParticipantSourceResult([], "Выберите сценарий — его НПС и твари появятся здесь.");

        var scenario = await scenarios.GetAsync(scenarioId, cancellationToken);
        List<ParticipantOption> options = [];
        foreach (var npc in scenario.Npcs)
        {
            var character = npc.Character;
            var side = EncounterParticipants.SideOf(npc.Role);
            options.Add(new ParticipantOption(
                $"character:{character.Id}",
                character.Name,
                string.Join(" · ", new[] { "НПС", Core.Scenarios.ScenarioText.Of(npc.Role), character.Occupation, npc.Count > 1 ? $"×{npc.Count}" : null }
                    .Where(s => !string.IsNullOrWhiteSpace(s))),
                ParticipantKind.Npc,
                character.Id,
                AllowCount: true,
                async (count, ct) =>
                {
                    var sheet = (await characters.GetAsync(character.Id, ct)).Sheet;
                    if (count <= 1)
                        return [EncounterParticipants.FromSheet(character.Id, CharacterKind.Npc, sheet, context.Catalog, side)];

                    return
                    [
                        .. Enumerable.Range(0, count).Select(_ =>
                        {
                            var extra = EncounterParticipants.FromSheet(character.Id, CharacterKind.Npc, sheet, context.Catalog, side);
                            extra.SourceCharacterId = null;
                            extra.Note = "Статист: урон в лист не записывается";
                            return extra;
                        }),
                    ];
                })
            {
                DefaultCount = npc.Count,
            });
        }

        foreach (var creature in scenario.Creatures)
        {
            options.Add(new ParticipantOption(
                $"scenario-creature:{creature.Id}",
                creature.Name,
                string.Join(" · ", new[]
                {
                    "тварь",
                    creature.HasOwnStatblock ? "статблок сценария" : creature.CatalogName,
                    $"ПЗ {creature.Statblock.HitPoints}",
                    creature.LocationNote,
                    creature.Count > 1 ? $"×{creature.Count}" : null,
                }.Where(s => !string.IsNullOrWhiteSpace(s))),
                ParticipantKind.Creature,
                CharacterId: null,
                AllowCount: true,
                (count, _) => Task.FromResult<IReadOnlyList<EncounterParticipant>>(
                    [.. Enumerable.Range(0, Math.Max(1, count)).Select(_ => EncounterParticipants.FromStatblock(creature.CreatureId, creature.Name, creature.Statblock))]))
            {
                DefaultCount = creature.Count,
            });
        }

        return new ParticipantSourceResult(options, options.Count == 0 ? "В составе сценария нет НПС и тварей." : null);
    }
}
