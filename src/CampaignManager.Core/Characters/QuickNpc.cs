using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Characters;

/// <summary>
/// Боевой навык персонажа Хранителя, придуманного по ходу игры (гл. 10): неопытный — 25%, трактирный забияка
/// или наёмный головорез — 40%, профессиональный убийца — 70%. Значение члена — процент.
/// </summary>
public enum QuickNpcCombatLevel
{
    Novice = 25,
    Brawler = 40,
    Professional = 70,
}

/// <summary>Строка ключевого навыка формы: навык справочника (<see cref="SkillId"/>) или свой (<see cref="Name"/>).</summary>
public sealed class QuickNpcSkill
{
    public Guid? SkillId { get; set; }

    /// <summary>Только у своего навыка, которого нет в справочнике.</summary>
    public string? Name { get; set; }

    public int Value { get; set; }
}

/// <summary>
/// Форма «Быстрого НПС»: встречный констебль или бармен, которому нужны имя, род занятий, пара чисел и боевой
/// навык, а не полный лист сыщика. Лист из неё собирает <see cref="SheetBuilder.QuickNpc"/>; форма ничего не считает.
/// </summary>
public sealed class QuickNpcDraft
{
    public string Name { get; set; } = "";

    public NameGender Gender { get; set; } = NameGender.Male;

    public string Occupation { get; set; } = "";

    public int Age { get; set; } = 35;

    /// <summary>По умолчанию — «средний человек»: все по 50 (Комплекция 0, БкУ 0, 10 ПЗ).</summary>
    public Characteristics Characteristics { get; set; } = QuickNpcRules.TypicalCharacteristics();

    public int Luck { get; set; } = QuickNpcRules.TypicalLuck;

    /// <summary>Ключевые навыки — три-пять строк; остальное на листе остаётся на базе справочника.</summary>
    public List<QuickNpcSkill> Skills { get; set; } = [];

    public QuickNpcCombatLevel CombatLevel { get; set; } = QuickNpcCombatLevel.Novice;

    /// <summary>Оружие из справочника — в лист уходит копия (<see cref="SheetCopies.Weapon"/>).</summary>
    public WeaponData? Weapon { get; set; }

    /// <summary>Навык, который форма добавила под выбранное оружие, — чтобы убрать его при смене оружия.</summary>
    public Guid? WeaponSkillId { get; set; }

    /// <summary>Короткая заметка «кто он и что знает» — ложится в предысторию листа.</summary>
    public string Note { get; set; } = "";

    /// <summary>Типаж, из которого заполнена форма; только для подсветки кнопки.</summary>
    public string? ArchetypeKey { get; set; }
}

/// <summary>
/// Типаж: род занятий, ключевые навыки (по <b>коду</b> справочника — не по имени) и опытность в бою. Навыки,
/// связанные с занятием, — от 50% (гл. 10); боевой навык добавляется по <see cref="Combat"/>.
/// </summary>
public sealed record QuickNpcArchetype(
    string Key,
    string Label,
    string Icon,
    string Occupation,
    QuickNpcCombatLevel Combat,
    IReadOnlyList<(string Code, int Value)> Skills,
    bool CarriesHandgun = false);

/// <summary>
/// «Быстрый НПС» (гл. 10, стр. 187–189): книга прямо говорит, что такому персонажу полный лист не нужен —
/// навык по занятию от 50%, прочие ниже, боевой 25/40/70%, имя из готового списка под эпоху. Лист при этом
/// собирается настоящий тем же <see cref="SheetBuilder"/>, что у помощника: навыки — ссылки на справочник (в v1 —
/// захардкоженный набор со старыми именами), производные — правила, родной язык — ОБР формулой справочника.
/// </summary>
public static class QuickNpcRules
{
    public const int TypicalCharacteristic = 50;

    public const int TypicalLuck = 50;

    /// <summary>Сколько строк ключевых навыков помещается в форму.</summary>
    public const int MaxSkillRows = 8;

    public static IReadOnlyList<(QuickNpcCombatLevel Level, string Label)> CombatLevels { get; } =
    [
        (QuickNpcCombatLevel.Novice, "Неопытный"),
        (QuickNpcCombatLevel.Brawler, "Забияка"),
        (QuickNpcCombatLevel.Professional, "Профи"),
    ];

    public static IReadOnlyList<QuickNpcArchetype> Archetypes { get; } =
    [
        new("police", "Полицейский", "fa-user-shield", "Полицейский", QuickNpcCombatLevel.Brawler,
            [("skill.spot-hidden", 50), (SkillCodes.Intimidate, 50), ("skill.law", 30)], CarriesHandgun: true),
        new("thug", "Громила", "fa-hand-fist", "Громила", QuickNpcCombatLevel.Brawler,
            [(SkillCodes.Intimidate, 50), ("skill.stealth", 30), ("skill.spot-hidden", 30)]),
        new("barkeep", "Бармен", "fa-martini-glass", "Бармен", QuickNpcCombatLevel.Novice,
            [("skill.listen", 50), ("skill.psychology", 50), (SkillCodes.Charm, 40)]),
        new("servant", "Прислуга", "fa-bell-concierge", "Прислуга", QuickNpcCombatLevel.Novice,
            [("skill.spot-hidden", 40), ("skill.listen", 40), ("skill.stealth", 35)]),
        new("reporter", "Журналист", "fa-newspaper", "Журналист", QuickNpcCombatLevel.Novice,
            [(SkillCodes.FastTalk, 50), ("skill.library-use", 50), ("skill.psychology", 40), ("skill.spot-hidden", 40)]),
        new("doctor", "Врач", "fa-user-doctor", "Врач", QuickNpcCombatLevel.Novice,
            [("skill.medicine", 60), ("skill.first-aid", 60), ("skill.psychology", 40)]),
        new("scholar", "Учёный", "fa-book", "Библиотекарь", QuickNpcCombatLevel.Novice,
            [("skill.library-use", 60), ("skill.history", 50), ("skill.occult", 30)]),
        new("cultist", "Культист", "fa-skull", "Культист", QuickNpcCombatLevel.Brawler,
            [("skill.occult", 40), ("skill.stealth", 40), (SkillCodes.Persuade, 35), (SkillCodes.Mythos, 10)]),
        new("killer", "Убийца", "fa-user-ninja", "Наёмный убийца", QuickNpcCombatLevel.Professional,
            [("skill.stealth", 60), ("skill.spot-hidden", 50)], CarriesHandgun: true),
        new("bystander", "Обыватель", "fa-user", "Обыватель", QuickNpcCombatLevel.Novice,
            [("skill.spot-hidden", 30), ("skill.listen", 30)]),
    ];

    public const string BrawlCode = "skill.fighting.brawl";

    public const string HandgunCode = "skill.firearms.handgun";

    public static Characteristics TypicalCharacteristics() => new()
    {
        Str = TypicalCharacteristic, Con = TypicalCharacteristic, Siz = TypicalCharacteristic, Dex = TypicalCharacteristic,
        App = TypicalCharacteristic, Int = TypicalCharacteristic, Pow = TypicalCharacteristic, Edu = TypicalCharacteristic,
    };

    /// <summary>Бросок характеристик формулами главы 3 (стр. 28–29) — тем же правилом, что у помощника.</summary>
    public static Characteristics RollCharacteristics(IDiceRoller dice)
    {
        var result = new Characteristics();
        foreach (var info in InvestigatorCreationRules.Characteristics)
            result[info.Key] = InvestigatorCreationRules.Roll(info.Key, dice).Value;
        return result;
    }

    /// <summary>
    /// Заполняет форму типажом: род занятий, ключевые навыки, опытность в бою. Имя, характеристики и оружие не
    /// трогает — их Хранитель мог уже выбрать. Навык типажа, которого нет в справочнике, пропускается.
    /// </summary>
    public static void ApplyArchetype(QuickNpcDraft draft, QuickNpcArchetype archetype, SkillCatalog catalog)
    {
        draft.ArchetypeKey = archetype.Key;
        draft.Occupation = archetype.Occupation;
        draft.Skills =
        [
            .. archetype.Skills
                .Select(s => (Skill: catalog.FindByCode(s.Code), s.Value))
                .Where(s => s.Skill is not null)
                .Select(s => new QuickNpcSkill { SkillId = s.Skill!.Id, Value = s.Value }),
        ];

        // Строка оружия переживает смену типажа: оружие выбрано отдельно от него.
        var weaponSkill = draft.WeaponSkillId;
        draft.WeaponSkillId = null;

        ApplyCombatLevel(draft, archetype.Combat, catalog);

        if (archetype.CarriesHandgun && catalog.FindByCode(HandgunCode) is { } handgun)
            Upsert(draft, handgun.Id, (int)draft.CombatLevel);

        if (weaponSkill is { } id)
        {
            Upsert(draft, id, (int)draft.CombatLevel);
            draft.WeaponSkillId = id;
        }
    }

    /// <summary>
    /// Опытность в бою: все боевые строки формы получают её значение, а драка добавляется, если её ещё нет, —
    /// без неё НПС нечем ответить на удар.
    /// </summary>
    public static void ApplyCombatLevel(QuickNpcDraft draft, QuickNpcCombatLevel level, SkillCatalog catalog)
    {
        draft.CombatLevel = level;

        foreach (var row in draft.Skills.Where(r => IsCombatSkill(r.SkillId, catalog)))
            row.Value = (int)level;

        if (catalog.FindByCode(BrawlCode) is { } brawl && draft.Skills.All(r => r.SkillId != brawl.Id))
            draft.Skills.Add(new QuickNpcSkill { SkillId = brawl.Id, Value = (int)level });
    }

    /// <summary>
    /// Выбранное оружие: навык, которым из него бьют (ссылка справочника оружия), получает боевое значение. Навык
    /// прошлого оружия убирается, если его добавила форма, а не Хранитель или типаж.
    /// </summary>
    public static void ApplyWeapon(QuickNpcDraft draft, WeaponData? weapon, SkillCatalog catalog)
    {
        if (draft.WeaponSkillId is { } previous && catalog.CodeOf(previous) != BrawlCode)
            draft.Skills.RemoveAll(r => r.SkillId == previous);

        draft.Weapon = weapon;
        draft.WeaponSkillId = null;

        if (weapon?.SkillId is not { } skillId || catalog.Find(skillId) is null)
            return;

        var alreadyThere = draft.Skills.Any(r => r.SkillId == skillId);
        Upsert(draft, skillId, (int)draft.CombatLevel);
        if (!alreadyThere)
            draft.WeaponSkillId = skillId;
    }

    /// <summary>Боевые навыки — те, что поднимает опытность: ближний бой, стрельба, метание (по коду).</summary>
    public static bool IsCombatSkill(Guid? skillId, SkillCatalog catalog)
    {
        var code = catalog.CodeOf(skillId) ?? catalog.CodeOf(catalog.Find(skillId)?.ParentId);
        return code is not null
               && (code == "skill.throw"
                   || code == SkillCodes.Fighting || code.StartsWith(SkillCodes.Fighting + ".", StringComparison.Ordinal)
                   || code == SkillCodes.Firearms || code.StartsWith(SkillCodes.Firearms + ".", StringComparison.Ordinal));
    }

    public static int ClampSkill(int value) => Math.Clamp(value, 0, 99);

    private static void Upsert(QuickNpcDraft draft, Guid skillId, int value)
    {
        var row = draft.Skills.FirstOrDefault(r => r.SkillId == skillId);
        if (row is null)
            draft.Skills.Add(new QuickNpcSkill { SkillId = skillId, Value = value });
        else
            row.Value = Math.Max(row.Value, value);
    }
}
