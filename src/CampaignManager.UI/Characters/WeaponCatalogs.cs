using System.Globalization;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Core.Characters;

namespace CampaignManager.UI.Characters;

/// <summary>Оружие справочника API как <see cref="WeaponData"/> Core — одна раскладка для листа и быстрого НПС.</summary>
public static class WeaponCatalogs
{
    public static WeaponData Data(WeaponDto weapon) => new(weapon.Id, weapon.Name)
    {
        SkillId = weapon.SkillId,
        Damage = weapon.Damage,
        Range = weapon.Range,
        Attacks = weapon.Attacks,
        Ammo = weapon.Ammo,
        Malfunction = weapon.Malfunction is { } m ? (m == 100 ? "00" : m.ToString(CultureInfo.InvariantCulture)) : "",
        Impaling = weapon.IsImpaling,
    };
}
