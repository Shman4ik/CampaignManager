using CampaignManager.Core.Characters;

namespace CampaignManager.Core.Catalogs;

/// <summary>
/// Числа оружия на листе — всегда разбор его текста при чтении. В v1 у копии на листе были ещё и
/// разобранные поля, они не пересчитывались после правки, и бой бросал устаревший урон (AUDIT,
/// «Персонажи и НПС → Ошибки», п. 2); ступени «разобранное поле → строка» больше нет.
/// </summary>
public static class WeaponStatsReader
{
    public static DamageInfo Damage(SheetWeapon? weapon) => DamageFormulaParser.Parse(weapon?.Damage);

    public static WeaponRangeInfo Range(SheetWeapon? weapon) => WeaponStatsParser.ParseRange(weapon?.Range);

    public static WeaponAttacksInfo Attacks(SheetWeapon? weapon) => WeaponStatsParser.ParseAttacks(weapon?.Attacks);

    public static WeaponAmmoInfo Ammo(SheetWeapon? weapon) => WeaponStatsParser.ParseAmmo(weapon?.Ammo);

    /// <summary>Ёмкость магазина; null — без магазина, лента или не разобрано.</summary>
    public static int? AmmoCapacity(SheetWeapon? weapon) => Ammo(weapon).Capacity is > 0 and var c ? c : null;

    /// <summary>Порог осечки (стр. 113): бросок ≥ порога — заклинило. False — осечки нет или порог вне 1–100.</summary>
    public static bool TryMalfunctionThreshold(SheetWeapon? weapon, out int threshold)
    {
        var value = WeaponStatsParser.ParseMalfunction(weapon?.Malfunction);
        threshold = value is >= 1 and <= 100 ? value.Value : 0;
        return threshold > 0;
    }

    /// <summary>Базовая дальность в метрах; null у ближнего боя, метательного и неразобранного.</summary>
    public static int? BaseRangeMeters(SheetWeapon? weapon) => Range(weapon).BaseMeters is > 0 and var m ? m : null;

    /// <summary>Выстрелов за раунд при обычной стрельбе; null — только очередями или не разобрано.</summary>
    public static int? ShotsPerRound(SheetWeapon? weapon) => Attacks(weapon).ShotsPerRound;

    /// <summary>Предел за одну проверку («1 (3)» → 3), иначе обычное число выстрелов.</summary>
    public static int? MaxShotsPerRound(SheetWeapon? weapon)
    {
        var attacks = Attacks(weapon);
        return attacks.MaxShotsPerRound ?? attacks.ShotsPerRound;
    }

    /// <summary>Число атак для показа: сводка разобранного либо исходный текст.</summary>
    public static string AttacksDisplay(SheetWeapon? weapon) => AttacksDisplay(Attacks(weapon));

    public static string AttacksDisplay(WeaponAttacksInfo attacks)
    {
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

    /// <summary>Дальность для показа: сводка разобранного либо исходный текст.</summary>
    public static string RangeDisplay(SheetWeapon? weapon) => RangeDisplay(Range(weapon));

    public static string RangeDisplay(WeaponRangeInfo range) => range switch
    {
        { IsParsed: false } => range.RawText,
        { Kind: WeaponRangeKind.Touch } => "касание",
        { Kind: WeaponRangeKind.Emplaced } => "на месте",
        { Kind: WeaponRangeKind.StrengthThrow, ThrowDivisor: { } divisor } => $"СИЛ / {divisor} м",
        { Kind: WeaponRangeKind.RangeBands, Bands: { Count: > 0 } bands } => $"{string.Join("/", bands)} м",
        { Kind: WeaponRangeKind.Meters, BaseMeters: { } meters } => $"{meters} м",
        _ => range.RawText,
    };
}
