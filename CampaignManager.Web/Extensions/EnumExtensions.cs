using CampaignManager.Web.Components.Features.Bestiary.Model;
using CampaignManager.Web.Components.Features.Books.Model;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Skills.Model;
using CampaignManager.Web.Components.Features.Weapons.Model;

namespace CampaignManager.Web.Extensions;

public static class EnumExtensions
{
    public static string ToRussianString(this WeaponType type)
    {
        return type switch
        {
            WeaponType.Melee => "Холодное оружие",
            WeaponType.Pistols => "Пистолеты",
            WeaponType.Rifles => "Винтовки",
            WeaponType.Shotguns => "Дробовики",
            WeaponType.AssaultRifles => "Автоматические винтовки",
            WeaponType.SubmachineGuns => "Пистолеты-пулемёты",
            WeaponType.MachineGuns => "Пулемёты",
            WeaponType.ExplosivesAndHeavyWeapons => "Взрывчатка/Тяжёлое", // Shorter for display
            WeaponType.Other => "Другое",
            _ => type.ToString() // Fallback
        };
    }

    public static string ToRussianString(this CreatureType type)
    {
        return type switch
        {
            CreatureType.MythicMonsters => "Миф. монстры",
            CreatureType.MythicGods => "Миф. боги",
            CreatureType.Monsters => "Монстры",
            CreatureType.Beast => "Животные",
            CreatureType.Other => "Другое",
            _ => type.ToString()
        };
    }

    public static string ToRussianString(this SkillCategory category)
    {
        return category switch
        {
            SkillCategory.ProblemSolving => "Решение проблем",
            SkillCategory.InformationGathering => "Сбор информации",
            SkillCategory.Special => "Специальные",
            SkillCategory.Social => "Социальные",
            SkillCategory.Healing => "Лечение",
            SkillCategory.CombatGeneral => "Общее сражение",
            SkillCategory.Knowledge => "Знания",
            SkillCategory.CombatFirearms => "Огнестрельное",
            SkillCategory.Actions => "Действия",
            _ => category.ToString()
        };
    }

    // Helper to get all enum values for dropdowns, etc.
    public static IEnumerable<WeaponType> GetWeaponTypes()
    {
        return Enum.GetValues(typeof(WeaponType)).Cast<WeaponType>();
    }

    public static IEnumerable<T> GetEnumTypes<T>() where T : Enum
    {
        return Enum.GetValues(typeof(T)).Cast<T>();
    }

    public static string ToRussianString(this OccupationSkillPointFormula formula)
    {
        return formula switch
        {
            OccupationSkillPointFormula.Edu4 => "ОБР × 4",
            OccupationSkillPointFormula.Edu2Dex2 => "ОБР × 2 + ЛВК × 2",
            OccupationSkillPointFormula.Edu2App2 => "ОБР × 2 + НАР × 2",
            OccupationSkillPointFormula.Edu2Str2 => "ОБР × 2 + СИЛ × 2",
            OccupationSkillPointFormula.Edu2Pow2 => "ОБР × 2 + МОЩ × 2",
            // Книга даёт игроку выбор, а не «наибольшее из» (стр. 38–39), поэтому и пишем «или».
            OccupationSkillPointFormula.Edu2DexOrStr2 => "ОБР × 2 + ЛВК × 2 или ОБР × 2 + СИЛ × 2",
            OccupationSkillPointFormula.Edu2AppOrPow2 => "ОБР × 2 + НАР × 2 или ОБР × 2 + МОЩ × 2",
            OccupationSkillPointFormula.Edu2DexOrPow2 => "ОБР × 2 + ЛВК × 2 или ОБР × 2 + МОЩ × 2",
            OccupationSkillPointFormula.Edu2AppOrDexOrStr2 => "ОБР × 2 + НАР × 2, ЛВК × 2 или СИЛ × 2",
            _ => formula.ToString()
        };
    }

    public static string ToRussianString(this NpcRole role)
    {
        return role switch
        {
            NpcRole.Neutral => "Нейтральный",
            NpcRole.Enemy => "Враг",
            NpcRole.Ally => "Союзник",
            _ => role.ToString()
        };
    }

    public static string ToRussianString(this CharacterKind kind) => kind switch
    {
        CharacterKind.PlayerCharacter => "Персонаж игрока",
        CharacterKind.Pregen => "Преген",
        CharacterKind.Npc => "НПС",
        _ => kind.ToString()
    };

    public static string ToRussianString(this CombatSide side) => side switch
    {
        CombatSide.Party => "отряд",
        CombatSide.Enemy => "противник",
        CombatSide.Neutral => "нейтральный",
        _ => side.ToString()
    };

    public static string ToRussianString(this BookType type) => type switch
    {
        BookType.MythosBook => "Книга Мифов",
        BookType.OccultBook => "Книга по оккультизму",
        _ => type.ToString()
    };
}