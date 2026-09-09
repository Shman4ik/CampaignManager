using CampaignManager.Web.Components.Features.Weapons.Model;

namespace CampaignManager.Web.Components.Features.Weapons.Services;

/// <summary>
///     Колонка «Встречается» таблицы XVII одной строкой. Редкость ортогональна эпохе,
///     поэтому «1920-е, редко» — это два флага, а не третье значение.
/// </summary>
public static class WeaponEraText
{
    public static string Describe(Weapon? weapon)
    {
        if (weapon is null) return "—";

        List<string> parts = [];
        if (weapon.Is1920) parts.Add("1920-е");
        if (weapon.IsModern) parts.Add("наши дни");
        if (weapon.IsRare) parts.Add("редко");

        return parts.Count == 0 ? "—" : string.Join(", ", parts);
    }
}
