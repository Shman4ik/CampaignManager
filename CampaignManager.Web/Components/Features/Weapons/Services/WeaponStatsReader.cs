using CampaignManager.Web.Components.Features.Weapons.Model;
using CampaignManager.Web.Utilities.Services;

namespace CampaignManager.Web.Components.Features.Weapons.Services;

/// <summary>
/// Единственная точка, где потребители спрашивают у оружия число.
/// <para>
/// Порядок всегда один: разобранное поле (<c>RangeInfo</c>, <c>AttacksInfo</c>,
/// <c>AmmoInfo</c>, <c>MalfunctionThreshold</c>) → разбор строки на лету → пусто.
/// Средняя ступень нужна из-за старых копий оружия внутри JSONB листов персонажей:
/// они были записаны до появления разобранных полей и несут только текст.
/// </para>
/// </summary>
public static class WeaponStatsReader
{
    /// <summary>
    /// Ёмкость магазина; null — оружие без магазина либо разобрать не удалось.
    /// Ленточное питание («Автоподача») ёмкости не имеет — тоже null.
    /// </summary>
    public static int? AmmoCapacity(Weapon? weapon)
    {
        if (weapon is null) return null;

        var info = weapon.AmmoInfo ?? WeaponStatsParser.ParseAmmo(weapon.Ammo);
        return info.Capacity is > 0 ? info.Capacity : null;
    }

    /// <summary>
    /// Порог осечки (стр. 113): бросок ≥ порога — оружие заклинило.
    /// False, когда осечки у оружия нет или порог вне диапазона 1–100.
    /// </summary>
    public static bool TryMalfunctionThreshold(Weapon? weapon, out int threshold)
    {
        threshold = 0;
        if (weapon is null) return false;

        var value = weapon.MalfunctionThreshold ?? WeaponStatsParser.ParseMalfunction(weapon.Malfunction);
        if (value is not (>= 1 and <= 100)) return false;

        threshold = value.Value;
        return true;
    }

    /// <summary>
    /// Базовая дальность в метрах; null для оружия ближнего боя, метательного
    /// (дальность считается от СИЛ) и там, где разобрать не удалось.
    /// </summary>
    public static int? BaseRangeMeters(Weapon? weapon)
    {
        if (weapon is null) return null;

        var info = weapon.RangeInfo ?? WeaponStatsParser.ParseRange(weapon.Range);
        return info.BaseMeters is > 0 ? info.BaseMeters : null;
    }

    /// <summary>Разобранная дальность — всегда объект, даже если разбор не удался.</summary>
    public static WeaponRangeInfo Range(Weapon? weapon) =>
        weapon?.RangeInfo ?? WeaponStatsParser.ParseRange(weapon?.Range);

    /// <summary>Разобранное число атак — всегда объект, даже если разбор не удался.</summary>
    public static WeaponAttacksInfo Attacks(Weapon? weapon) =>
        weapon?.AttacksInfo ?? WeaponStatsParser.ParseAttacks(weapon?.Attacks);

    /// <summary>Разобранный боезапас — всегда объект, даже если разбор не удался.</summary>
    public static WeaponAmmoInfo Ammo(Weapon? weapon) =>
        weapon?.AmmoInfo ?? WeaponStatsParser.ParseAmmo(weapon?.Ammo);

    /// <summary>Разобранная стоимость — всегда объект, даже если разбор не удался.</summary>
    public static WeaponCostInfo Cost(Weapon? weapon) =>
        weapon?.CostInfo ?? WeaponStatsParser.ParseCost(weapon?.Cost);

    /// <summary>
    /// Сколько выстрелов оружие делает за раунд при обычной стрельбе.
    /// Null — оружие стреляет только очередями либо разбор не удался.
    /// </summary>
    public static int? ShotsPerRound(Weapon? weapon) => Attacks(weapon).ShotsPerRound;

    /// <summary>
    /// Предел выстрелов за одну проверку: «1 (3)» → 3. Откатывается на
    /// <see cref="ShotsPerRound" />, когда отдельного предела нет.
    /// </summary>
    public static int? MaxShotsPerRound(Weapon? weapon)
    {
        var attacks = Attacks(weapon);
        return attacks.MaxShotsPerRound ?? attacks.ShotsPerRound;
    }

    /// <summary>Строка числа атак для показа: разобранная сводка либо исходный текст.</summary>
    public static string AttacksDisplay(Weapon? weapon)
    {
        var attacks = Attacks(weapon);
        if (!attacks.IsParsed) return attacks.RawText;

        List<string> parts = [];

        if (attacks.RoundsPerAttack is > 1 && attacks.ShotsPerRound is { } perRound)
            parts.Add($"{perRound}/{attacks.RoundsPerAttack}");
        else if (attacks.ShotsPerRound is { } shots)
            parts.Add(attacks.MaxShotsPerRound is { } max && max != shots ? $"{shots} ({max})" : $"{shots}");

        if (attacks.AllowsBurst) parts.Add(attacks.BurstSize is { } size ? $"очередь по {size}" : "очередь");
        if (attacks.AllowsFullAuto) parts.Add("непр. огонь");
        if (attacks.IsSingleUse) parts.Add("однораз.");

        return parts.Count == 0 ? attacks.RawText : string.Join(" · ", parts);
    }

    /// <summary>Строка дальности для показа: разобранная сводка либо исходный текст.</summary>
    public static string RangeDisplay(Weapon? weapon)
    {
        var range = Range(weapon);

        return range switch
        {
            { IsParsed: false } => range.RawText,
            { Kind: WeaponRangeKind.Touch } => "касание",
            { Kind: WeaponRangeKind.Emplaced } => "на месте",
            { Kind: WeaponRangeKind.StrengthThrow, ThrowDivisor: { } divisor } => $"СИЛ / {divisor} м",
            { Kind: WeaponRangeKind.RangeBands, Bands: { Count: > 0 } bands } => $"{string.Join("/", bands)} м",
            { Kind: WeaponRangeKind.Meters, BaseMeters: { } meters } => $"{meters} м",
            _ => range.RawText
        };
    }
}
