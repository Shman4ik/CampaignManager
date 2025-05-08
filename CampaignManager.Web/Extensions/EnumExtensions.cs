using CampaignManager.Web.Components.Features.Bestiary.Model;
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

    // Helper to get all enum values for dropdowns, etc.
    public static IEnumerable<WeaponType> GetWeaponTypes()
    {
        return Enum.GetValues(typeof(WeaponType)).Cast<WeaponType>();
    }

    public static IEnumerable<T> GetEnumTypes<T>() where T : Enum
    {
        return Enum.GetValues(typeof(T)).Cast<T>();
    }
}