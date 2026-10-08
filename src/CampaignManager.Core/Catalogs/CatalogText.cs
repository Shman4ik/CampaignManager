using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Catalogs;

/// <summary>
/// Русские подписи перечислений справочников — одна таблица на приложение. В v1 у категорий навыков
/// было два словаря (<c>EnumExtensions</c> и <c>SkillService.CategoryGroupNames</c>) с разными
/// написаниями, а у эпохи — четыре подписи на четырёх страницах.
/// </summary>
public static class CatalogText
{
    /// <summary>Группа навыков на бланке сыщика: «Сбор информации», «Сражение (огнестрельное)».</summary>
    public static string Of(SkillCategory category) => category switch
    {
        SkillCategory.ProblemSolving => "Решение проблем",
        SkillCategory.InformationGathering => "Сбор информации",
        SkillCategory.Special => "Специальные",
        SkillCategory.Social => "Социальные",
        SkillCategory.Healing => "Лечение",
        SkillCategory.CombatGeneral => "Сражение (общее)",
        SkillCategory.Knowledge => "Знания",
        SkillCategory.CombatFirearms => "Сражение (огнестрельное)",
        SkillCategory.Actions => "Действия",
        _ => category.ToString(),
    };

    /// <summary>Эпоха — одна подпись везде: «1920-е» / «Наши дни».</summary>
    public static string Of(Era era) => era switch
    {
        Era.Classic => "1920-е",
        Era.Modern => "Наши дни",
        _ => era.ToString(),
    };

    /// <summary>Эпохи записи строкой; все эпохи — «любая».</summary>
    public static string Of(IReadOnlyCollection<Era> eras) =>
        eras.Count == 0 || eras.Count == Enum.GetValues<Era>().Length
            ? "любая"
            : string.Join(", ", eras.Order().Select(Of));

    /// <summary>Раздел таблицы XVII.</summary>
    public static string Of(WeaponType type) => type switch
    {
        WeaponType.Melee => "Холодное",
        WeaponType.Pistols => "Пистолеты",
        WeaponType.Rifles => "Винтовки",
        WeaponType.Shotguns => "Дробовики",
        WeaponType.AssaultRifles => "Автоматы",
        WeaponType.SubmachineGuns => "Пистолеты-пулемёты",
        WeaponType.MachineGuns => "Пулемёты",
        WeaponType.ExplosivesAndHeavyWeapons => "Взрывчатка и тяжёлое",
        _ => "Другое",
    };

    public static string Of(CreatureType type) => type switch
    {
        CreatureType.MythicMonsters => "Монстры Мифов",
        CreatureType.MythicGods => "Божества Мифов",
        CreatureType.Monsters => "Чудовища",
        CreatureType.Beast => "Животные",
        _ => "Другое",
    };

    public static string Of(ArtifactKind kind) => kind switch
    {
        ArtifactKind.Device => "Устройство",
        ArtifactKind.Weapon => "Оружие",
        ArtifactKind.Armor => "Броня",
        ArtifactKind.Substance => "Снадобье",
        ArtifactKind.Relic => "Магический предмет",
        ArtifactKind.Place => "Место",
        _ => "Другое",
    };

    public static string Of(BookType type) => type == BookType.MythosBook ? "Книга Мифов" : "Оккультная книга";

    /// <summary>Метка категории в строке: полная подпись съедала половину колонки названия.</summary>
    public static string Short(BookType type) => type == BookType.MythosBook ? "Мифы" : "Оккультизм";

    /// <summary>Формула очков навыков профессии (стр. 38–39).</summary>
    public static string Of(SkillPointsFormula formula) => formula switch
    {
        SkillPointsFormula.Edu4 => "ОБР × 4",
        SkillPointsFormula.Edu2Dex2 => "ОБР × 2 + ЛВК × 2",
        SkillPointsFormula.Edu2App2 => "ОБР × 2 + НАР × 2",
        SkillPointsFormula.Edu2Str2 => "ОБР × 2 + СИЛ × 2",
        SkillPointsFormula.Edu2Pow2 => "ОБР × 2 + МОЩ × 2",
        SkillPointsFormula.Edu2DexOrStr2 => "ОБР × 2 + (ЛВК или СИЛ) × 2",
        SkillPointsFormula.Edu2AppOrPow2 => "ОБР × 2 + (НАР или МОЩ) × 2",
        SkillPointsFormula.Edu2DexOrPow2 => "ОБР × 2 + (ЛВК или МОЩ) × 2",
        _ => "ОБР × 2 + (НАР, ЛВК или СИЛ) × 2",
    };

    public static string Of(OccupationSlotKind kind) => kind switch
    {
        OccupationSlotKind.Skill => "Навык",
        OccupationSlotKind.Specialization => "Названная специализация",
        OccupationSlotKind.AnySpecialization => "Любая специализация",
        OccupationSlotKind.Choice => "Выбор из списка",
        OccupationSlotKind.Social => "Социальный навык",
        _ => "Любой навык",
    };

    public static string Of(CreatureAttackKind kind) => kind switch
    {
        CreatureAttackKind.Ranged => "дальний бой",
        CreatureAttackKind.Maneuver => "манёвр",
        CreatureAttackKind.Special => "особая",
        _ => "ближний бой",
    };

    /// <summary>Как бонус к урону входит в урон атаки; слово — из словаря (<see cref="Terms.DamageBonus"/>), не «БкУ».</summary>
    public static string Of(CreatureDamageBonusMode mode) => mode switch
    {
        CreatureDamageBonusMode.Full => $"+ {Terms.DamageBonus}",
        CreatureDamageBonusMode.Half => "+ ½ бонуса к урону",
        CreatureDamageBonusMode.OnlyBonus => "урон равен бонусу к урону",
        _ => "без бонуса к урону",
    };

    /// <summary>Урон атаки твари как в книге: «2d6 + бонус к урону»; «равен бонусу к урону» — своих костей нет (стр. 306).</summary>
    public static string Damage(CreatureAttack attack)
    {
        var dice = DiceNotation.Format(attack.Damage);
        return attack.DamageBonusMode switch
        {
            CreatureDamageBonusMode.OnlyBonus => "равен бонусу к урону",
            CreatureDamageBonusMode.Full => $"{dice} + {Terms.DamageBonus}",
            CreatureDamageBonusMode.Half => $"{dice} + ½ бонуса к урону",
            _ => dice,
        };
    }
}
