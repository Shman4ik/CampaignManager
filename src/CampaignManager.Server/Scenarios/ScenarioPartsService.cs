using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core;
using CampaignManager.Core.Scenarios;
using CampaignManager.Data;
using CampaignManager.Data.Scenarios;
using CampaignManager.Server.Access;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Server.Scenarios;

/// <summary>
/// Части сценария — по строке на запрос: локации, проверки, ключевые факты, раздатки, твари, предметы. Каждый метод
/// записи — <see cref="AccessPolicy.ForScenarioAsync"/> Edit, строка ищется в этом сценарии (чужая — 404, как и
/// несуществующая). Правка строки пишет только её колонки: соседние вкладки и корень не трогаются.
/// </summary>
public sealed class ScenarioPartsService(CmDbContext dbContext, AccessPolicy access)
{
    private static readonly IReadOnlyDictionary<Guid, string> NoSkills = new Dictionary<Guid, string>();

    // ── Локации ─────────────────────────────────────────────────────

    public async Task<ScenarioLocationDto> AddLocationAsync(Guid scenarioId, LocationInput input, CancellationToken cancellationToken)
    {
        await DemandEditAsync(scenarioId, cancellationToken);
        var location = new ScenarioLocation { ScenarioId = scenarioId, Name = "" };
        await ApplyLocationAsync(location, input, cancellationToken);
        location.Ord = await NextOrdAsync(dbContext.ScenarioLocations
            .Where(l => l.ScenarioId == scenarioId && l.ParentId == location.ParentId).Select(l => l.Ord), cancellationToken);
        dbContext.ScenarioLocations.Add(location);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await LocationDtoAsync(location.Id, cancellationToken);
    }

    public async Task<ScenarioLocationDto> UpdateLocationAsync(Guid scenarioId, Guid locationId, LocationInput input, CancellationToken cancellationToken)
    {
        await DemandEditAsync(scenarioId, cancellationToken);
        var location = await Find(dbContext.ScenarioLocations.Where(l => l.ScenarioId == scenarioId && l.Id == locationId), cancellationToken);
        var parentBefore = location.ParentId;
        await ApplyLocationAsync(location, input, cancellationToken);
        if (location.ParentId != parentBefore)
        {
            // В новом родителе — последней: место среди прежних соседей ничего не значит среди новых.
            location.Ord = await NextOrdAsync(dbContext.ScenarioLocations
                .Where(l => l.ScenarioId == scenarioId && l.ParentId == location.ParentId && l.Id != locationId).Select(l => l.Ord),
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await LocationDtoAsync(location.Id, cancellationToken);
    }

    /// <summary>Удалить локацию — с вложенными локациями и их проверками (каскад в базе).</summary>
    public async Task DeleteLocationAsync(Guid scenarioId, Guid locationId, CancellationToken cancellationToken)
    {
        await DemandEditAsync(scenarioId, cancellationToken);
        await DeleteOne(dbContext.ScenarioLocations.Where(l => l.ScenarioId == scenarioId && l.Id == locationId), cancellationToken);
    }

    // ── Проверки ────────────────────────────────────────────────────

    public async Task<ScenarioCheckDto> AddCheckAsync(Guid scenarioId, Guid locationId, CheckInput input, CancellationToken cancellationToken)
    {
        await DemandEditAsync(scenarioId, cancellationToken);
        if (!await dbContext.ScenarioLocations.AnyAsync(l => l.ScenarioId == scenarioId && l.Id == locationId, cancellationToken))
        {
            throw AccessDeniedException.NotFound();
        }

        var check = new ScenarioCheck { LocationId = locationId };
        await ApplyCheckAsync(check, input, cancellationToken);
        check.Ord = await NextOrdAsync(dbContext.ScenarioChecks.Where(c => c.LocationId == locationId).Select(c => c.Ord), cancellationToken);
        dbContext.ScenarioChecks.Add(check);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await CheckDtoAsync(check, cancellationToken);
    }

    public async Task<ScenarioCheckDto> UpdateCheckAsync(Guid scenarioId, Guid checkId, CheckInput input, CancellationToken cancellationToken)
    {
        await DemandEditAsync(scenarioId, cancellationToken);
        var check = await Find(ChecksOf(scenarioId).Where(c => c.Id == checkId), cancellationToken);
        await ApplyCheckAsync(check, input, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await CheckDtoAsync(check, cancellationToken);
    }

    public async Task DeleteCheckAsync(Guid scenarioId, Guid checkId, CancellationToken cancellationToken)
    {
        await DemandEditAsync(scenarioId, cancellationToken);
        await DeleteOne(ChecksOf(scenarioId).Where(c => c.Id == checkId), cancellationToken);
    }

    // ── Ключевые факты ──────────────────────────────────────────────

    public async Task<KeyFactDto> AddFactAsync(Guid scenarioId, KeyFactInput input, CancellationToken cancellationToken)
    {
        await DemandEditAsync(scenarioId, cancellationToken);
        var fact = new ScenarioKeyFact { ScenarioId = scenarioId, Title = "" };
        ApplyFact(fact, input);
        fact.Ord = await NextOrdAsync(dbContext.ScenarioKeyFacts.Where(f => f.ScenarioId == scenarioId).Select(f => f.Ord), cancellationToken);
        dbContext.ScenarioKeyFacts.Add(fact);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ScenarioParts.Fact(fact);
    }

    public async Task<KeyFactDto> UpdateFactAsync(Guid scenarioId, Guid factId, KeyFactInput input, CancellationToken cancellationToken)
    {
        await DemandEditAsync(scenarioId, cancellationToken);
        var fact = await Find(dbContext.ScenarioKeyFacts.Where(f => f.ScenarioId == scenarioId && f.Id == factId), cancellationToken);
        ApplyFact(fact, input);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ScenarioParts.Fact(fact);
    }

    public async Task DeleteFactAsync(Guid scenarioId, Guid factId, CancellationToken cancellationToken)
    {
        await DemandEditAsync(scenarioId, cancellationToken);
        await DeleteOne(dbContext.ScenarioKeyFacts.Where(f => f.ScenarioId == scenarioId && f.Id == factId), cancellationToken);
    }

    // ── Раздатки ────────────────────────────────────────────────────

    public async Task<HandoutDto> AddHandoutAsync(Guid scenarioId, HandoutInput input, CancellationToken cancellationToken)
    {
        await DemandEditAsync(scenarioId, cancellationToken);
        var handout = new ScenarioHandout { ScenarioId = scenarioId, Name = "" };
        await ApplyHandoutAsync(handout, input, cancellationToken);
        handout.Ord = await NextOrdAsync(dbContext.ScenarioHandouts.Where(h => h.ScenarioId == scenarioId).Select(h => h.Ord), cancellationToken);
        dbContext.ScenarioHandouts.Add(handout);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ScenarioParts.Handout(handout);
    }

    public async Task<HandoutDto> UpdateHandoutAsync(Guid scenarioId, Guid handoutId, HandoutInput input, CancellationToken cancellationToken)
    {
        await DemandEditAsync(scenarioId, cancellationToken);
        var handout = await Find(dbContext.ScenarioHandouts.Where(h => h.ScenarioId == scenarioId && h.Id == handoutId), cancellationToken);
        await ApplyHandoutAsync(handout, input, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ScenarioParts.Handout(handout);
    }

    /// <summary>Удалить раздатку. Файл остаётся в <c>files</c>: сироты — админке.</summary>
    public async Task DeleteHandoutAsync(Guid scenarioId, Guid handoutId, CancellationToken cancellationToken)
    {
        await DemandEditAsync(scenarioId, cancellationToken);
        await DeleteOne(dbContext.ScenarioHandouts.Where(h => h.ScenarioId == scenarioId && h.Id == handoutId), cancellationToken);
    }

    // ── Твари ───────────────────────────────────────────────────────

    /// <summary>Тварь из бестиария: ссылка, без копии статблока — исправление в бестиарии дойдёт до сценария.</summary>
    public async Task<ScenarioCreatureDto> AddCreatureAsync(Guid scenarioId, ScenarioCreatureInput input, CancellationToken cancellationToken)
    {
        await DemandEditAsync(scenarioId, cancellationToken);
        if (input.CreatureId is not { } creatureId || !await dbContext.Creatures.AnyAsync(c => c.Id == creatureId, cancellationToken))
        {
            throw ApiProblemException.Invalid("Выберите тварь из бестиария.");
        }

        var creature = new ScenarioCreature { ScenarioId = scenarioId, CreatureId = creatureId };
        ApplyCreature(creature, input);
        creature.Ord = await NextOrdAsync(dbContext.ScenarioCreatures.Where(c => c.ScenarioId == scenarioId).Select(c => c.Ord), cancellationToken);
        dbContext.ScenarioCreatures.Add(creature);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await CreatureDtoAsync(creature.Id, cancellationToken);
    }

    public async Task<ScenarioCreatureDto> UpdateCreatureAsync(Guid scenarioId, Guid rowId, ScenarioCreatureInput input, CancellationToken cancellationToken)
    {
        await DemandEditAsync(scenarioId, cancellationToken);
        var creature = await Find(dbContext.ScenarioCreatures.Where(c => c.ScenarioId == scenarioId && c.Id == rowId), cancellationToken);
        ApplyCreature(creature, input);
        if (input.ResetStatblock)
        {
            if (creature.CreatureId is null)
            {
                throw ApiProblemException.Invalid("Этой твари нет в бестиарии — её статблок живёт только в сценарии.");
            }

            creature.Statblock = null;
            creature.StatblockVersion = null;
        }

        if (creature.CreatureId is null && creature.Name is null)
        {
            throw ApiProblemException.Invalid("У твари без бестиария должно быть имя.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await CreatureDtoAsync(creature.Id, cancellationToken);
    }

    public async Task DeleteCreatureAsync(Guid scenarioId, Guid rowId, CancellationToken cancellationToken)
    {
        await DemandEditAsync(scenarioId, cancellationToken);
        await DeleteOne(dbContext.ScenarioCreatures.Where(c => c.ScenarioId == scenarioId && c.Id == rowId), cancellationToken);
    }

    // ── Предметы ────────────────────────────────────────────────────

    /// <summary>Предмет справочника или свой реквизит (имя и описание — только у сценария).</summary>
    public async Task<ScenarioItemDto> AddItemAsync(Guid scenarioId, ScenarioItemInput input, CancellationToken cancellationToken)
    {
        await DemandEditAsync(scenarioId, cancellationToken);
        if (input.ItemId is { } itemId && !await dbContext.Items.AnyAsync(i => i.Id == itemId, cancellationToken))
        {
            throw ApiProblemException.Invalid("Такого предмета в справочнике нет.");
        }

        var item = new ScenarioItem { ScenarioId = scenarioId, ItemId = input.ItemId };
        ApplyItem(item, input);
        item.Ord = await NextOrdAsync(dbContext.ScenarioItems.Where(i => i.ScenarioId == scenarioId).Select(i => i.Ord), cancellationToken);
        dbContext.ScenarioItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ItemDtoAsync(item.Id, cancellationToken);
    }

    public async Task<ScenarioItemDto> UpdateItemAsync(Guid scenarioId, Guid rowId, ScenarioItemInput input, CancellationToken cancellationToken)
    {
        await DemandEditAsync(scenarioId, cancellationToken);
        var item = await Find(dbContext.ScenarioItems.Where(i => i.ScenarioId == scenarioId && i.Id == rowId), cancellationToken);
        ApplyItem(item, input);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ItemDtoAsync(item.Id, cancellationToken);
    }

    public async Task DeleteItemAsync(Guid scenarioId, Guid rowId, CancellationToken cancellationToken)
    {
        await DemandEditAsync(scenarioId, cancellationToken);
        await DeleteOne(dbContext.ScenarioItems.Where(i => i.ScenarioId == scenarioId && i.Id == rowId), cancellationToken);
    }

    // ── Общее ───────────────────────────────────────────────────────

    private Task DemandEditAsync(Guid scenarioId, CancellationToken cancellationToken) =>
        access.ForScenarioAsync(scenarioId, cancellationToken).Demand(Operation.Edit);

    private IQueryable<ScenarioCheck> ChecksOf(Guid scenarioId) =>
        dbContext.ScenarioChecks.Where(c => dbContext.ScenarioLocations.Any(l => l.Id == c.LocationId && l.ScenarioId == scenarioId));

    private static async Task<T> Find<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        await query.SingleOrDefaultAsync(cancellationToken) ?? throw AccessDeniedException.NotFound();

    private static async Task DeleteOne<T>(IQueryable<T> query, CancellationToken cancellationToken)
    {
        if (await query.ExecuteDeleteAsync(cancellationToken) == 0)
        {
            throw AccessDeniedException.NotFound();
        }
    }

    private static async Task<int> NextOrdAsync(IQueryable<int> ords, CancellationToken cancellationToken) =>
        (await ords.Select(o => (int?)o).MaxAsync(cancellationToken) ?? -1) + 1;

    private async Task ApplyLocationAsync(ScenarioLocation location, LocationInput input, CancellationToken cancellationToken)
    {
        if (input.ParentId is { } parentId)
        {
            var parents = await dbContext.ScenarioLocations.AsNoTracking()
                .Where(l => l.ScenarioId == location.ScenarioId)
                .ToDictionaryAsync(l => l.Id, l => l.ParentId, cancellationToken);
            if (!parents.ContainsKey(parentId))
            {
                throw ApiProblemException.Invalid("Родительская локация — из этого же сценария.");
            }

            // Родитель не может быть самой локацией или её потомком: дерево замкнулось бы в кольцо.
            for (Guid? current = parentId; current is { } id; current = parents.GetValueOrDefault(id))
            {
                if (id == location.Id)
                {
                    throw ApiProblemException.Invalid("Локацию нельзя вложить в неё саму или в её же вложенную локацию.");
                }
            }
        }

        ApplyLocationFields(location, input);
    }

    /// <summary>Поля локации без проверки родителя по базе — её делает вызывающий (импорт ищет родителя по имени в файле).</summary>
    internal static void ApplyLocationFields(ScenarioLocation location, LocationInput input)
    {
        location.Name = ScenarioService.Required(input.Name, ScenarioLimits.NameLength, "Название локации");
        location.Address = ScenarioService.Text(input.Address, ScenarioLimits.ShortTextLength, "Адрес");
        location.Description = ScenarioService.Text(input.Description, ScenarioLimits.TextLength, "Описание локации");
        location.ParentId = input.ParentId;
    }

    private async Task ApplyCheckAsync(ScenarioCheck check, CheckInput input, CancellationToken cancellationToken)
    {
        if (input is { TargetKind: CheckTarget.Skill, SkillId: { } skillId }
            && !await dbContext.Skills.AnyAsync(s => s.Id == skillId, cancellationToken))
        {
            throw ApiProblemException.Invalid("Выберите навык из справочника.");
        }

        ApplyCheckFields(check, input);
    }

    /// <summary>Поля проверки; что навык есть в справочнике, проверяет вызывающий.</summary>
    internal static void ApplyCheckFields(ScenarioCheck check, CheckInput input)
    {
        if (!Enum.IsDefined(input.TargetKind) || !Enum.IsDefined(input.Difficulty)
                                              || input.Characteristic is { } c && !Enum.IsDefined(c))
        {
            throw ApiProblemException.Invalid("Неизвестный вид проверки, характеристика или сложность.");
        }

        switch (input.TargetKind)
        {
            case CheckTarget.Skill when input.SkillId is null:
                throw ApiProblemException.Invalid("Выберите навык из справочника.");
            case CheckTarget.Characteristic when input.Characteristic is null:
                throw ApiProblemException.Invalid("Выберите характеристику.");
        }

        check.TargetKind = input.TargetKind;
        check.SkillId = input.TargetKind is CheckTarget.Skill ? input.SkillId : null;
        check.Characteristic = input.TargetKind is CheckTarget.Characteristic ? input.Characteristic : null;
        check.Difficulty = input.Difficulty;
        check.OnSuccess = ScenarioService.Text(input.OnSuccess, ScenarioLimits.TextLength, "Итог успеха");
        check.OnFailure = ScenarioService.Text(input.OnFailure, ScenarioLimits.TextLength, "Итог провала");
    }

    internal static void ApplyFact(ScenarioKeyFact fact, KeyFactInput input)
    {
        if (!Enum.IsDefined(input.Type))
        {
            throw ApiProblemException.Invalid("Неизвестный вид факта.");
        }

        fact.Type = input.Type;
        fact.Title = ScenarioService.Required(input.Title, ScenarioLimits.NameLength, "Заголовок факта");
        fact.Content = ScenarioService.Text(input.Content, ScenarioLimits.TextLength, "Содержание факта");
    }

    private async Task ApplyHandoutAsync(ScenarioHandout handout, HandoutInput input, CancellationToken cancellationToken)
    {
        if (input.FileId is { } fileId && !await dbContext.Files.AnyAsync(f => f.Id == fileId, cancellationToken))
        {
            throw ApiProblemException.Invalid("Файл раздатки не найден — загрузите его заново.");
        }

        ApplyHandoutFields(handout, input);
    }

    /// <summary>Поля раздатки; что файл есть, проверяет вызывающий.</summary>
    internal static void ApplyHandoutFields(ScenarioHandout handout, HandoutInput input)
    {
        handout.Name = ScenarioService.Required(input.Name, ScenarioLimits.NameLength, "Название раздатки");
        handout.PlayerText = ScenarioService.Text(input.PlayerText, ScenarioLimits.TextLength, "Текст для игроков");
        handout.KeeperNote = ScenarioService.Text(input.KeeperNote, ScenarioLimits.TextLength, "Пометка Хранителя");
        handout.FileId = input.FileId;
    }

    internal static void ApplyCreature(ScenarioCreature creature, ScenarioCreatureInput input)
    {
        if (input.Count is < 1 or > ScenarioLimits.MaxCount)
        {
            throw ApiProblemException.Invalid($"Количество — от 1 до {ScenarioLimits.MaxCount}.");
        }

        creature.Name = ScenarioService.Text(input.Name, ScenarioLimits.NameLength, "Имя твари");
        creature.Count = input.Count;
        creature.LocationNote = ScenarioService.Text(input.LocationNote, ScenarioLimits.ShortTextLength, "Где");
        creature.Notes = ScenarioService.Text(input.Notes, ScenarioLimits.TextLength, "Заметки");
    }

    internal static void ApplyItem(ScenarioItem item, ScenarioItemInput input)
    {
        item.Name = ScenarioService.Text(input.Name, ScenarioLimits.NameLength, "Название предмета");
        item.Description = ScenarioService.Text(input.Description, ScenarioLimits.TextLength, "Описание предмета");
        item.LocationNote = ScenarioService.Text(input.LocationNote, ScenarioLimits.ShortTextLength, "Где");
        item.Notes = ScenarioService.Text(input.Notes, ScenarioLimits.TextLength, "Заметки");
        if (item.ItemId is null && item.Name is null)
        {
            throw ApiProblemException.Invalid("У своего предмета должно быть название.");
        }
    }

    private async Task<ScenarioLocationDto> LocationDtoAsync(Guid locationId, CancellationToken cancellationToken)
    {
        var location = await dbContext.ScenarioLocations.AsNoTracking().Include(l => l.Checks)
            .SingleAsync(l => l.Id == locationId, cancellationToken);
        var tracks = await dbContext.LocationTracks.AsNoTracking().Where(t => t.LocationId == locationId).ToListAsync(cancellationToken);
        return ScenarioParts.Location(location, tracks, await SkillNamesAsync(location.Checks, cancellationToken));
    }

    private async Task<ScenarioCheckDto> CheckDtoAsync(ScenarioCheck check, CancellationToken cancellationToken) =>
        ScenarioParts.Check(check, await SkillNamesAsync([check], cancellationToken));

    private async Task<IReadOnlyDictionary<Guid, string>> SkillNamesAsync(IReadOnlyCollection<ScenarioCheck> checks, CancellationToken cancellationToken)
    {
        var ids = checks.Select(c => c.SkillId).OfType<Guid>().Distinct().ToList();
        return ids.Count == 0
            ? NoSkills
            : await dbContext.Skills.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);
    }

    private async Task<ScenarioCreatureDto> CreatureDtoAsync(Guid rowId, CancellationToken cancellationToken) =>
        (await ScenarioParts.CreaturesAsync(dbContext, dbContext.ScenarioCreatures.Where(c => c.Id == rowId), cancellationToken)).Single();

    private async Task<ScenarioItemDto> ItemDtoAsync(Guid rowId, CancellationToken cancellationToken) =>
        (await ScenarioParts.ItemsAsync(dbContext, dbContext.ScenarioItems.Where(i => i.Id == rowId), cancellationToken)).Single();
}
