using CampaignManager.Web.Weapons;
using System.ComponentModel.DataAnnotations;

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

    // Helper to get all enum values for dropdowns, etc.
    public static IEnumerable<WeaponType> GetWeaponTypes()
    {
        return Enum.GetValues(typeof(WeaponType)).Cast<WeaponType>();
    }
}
