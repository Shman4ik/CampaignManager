using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Catalogs;

/// <summary>Бонус к урону в формуле оружия.</summary>
public enum DamageBonusType
{
    /// <summary>Не добавляется (огнестрел).</summary>
    None,

    /// <summary>Полный (большинство оружия ближнего боя).</summary>
    Full,

    /// <summary>Половина (метательное, хлыст).</summary>
    Half,
}

/// <summary>
/// Разобранная формула урона: кости, плоская прибавка, бонус к урону и словесные эффекты. Бросает её
/// тот же <see cref="DiceTerm"/>, что и любую формулу костей.
/// </summary>
public sealed class DamageExpression
{
    public List<DiceTerm> Dice { get; set; } = [];

    public int FlatModifier { get; set; }

    public DamageBonusType DamageBonus { get; set; } = DamageBonusType.None;

    /// <summary>«горение», «шок» — на ПЗ не влияют.</summary>
    public List<string> Effects { get; set; } = [];

    public string RawText { get; set; } = string.Empty;

    public bool IsParsed { get; set; }

    /// <summary>Бросок без бонуса к урону — его добавляют отдельно.</summary>
    public int Roll(IDiceRoller roller) => Dice.Sum(d => d.Roll(roller)) + FlatModifier;

    /// <summary>Все кости на максимум — чрезвычайный успех.</summary>
    public int Max => Dice.Sum(d => d.Max) + FlatModifier;

    /// <summary>«2d6 +1 + Б.К.У.»</summary>
    public override string ToString()
    {
        if (!IsParsed)
            return RawText;

        List<string> parts = [.. Dice.Select(d => d.ToString())];

        if (FlatModifier != 0)
            parts.Add(FlatModifier > 0 ? $"+{FlatModifier}" : FlatModifier.ToString(System.Globalization.CultureInfo.InvariantCulture));

        if (DamageBonus == DamageBonusType.Full)
            parts.Add("+ Б.К.У.");
        else if (DamageBonus == DamageBonusType.Half)
            parts.Add("+ ½ Б.К.У.");

        parts.AddRange(Effects.Select(effect => $"+ {effect}"));

        return parts.Count == 0 ? "0" : string.Join(" ", parts).TrimStart('+', ' ');
    }
}

/// <summary>Урон на одной дальности (дробовик).</summary>
public sealed class RangeDamageEntry
{
    public string RangeLabel { get; set; } = string.Empty;

    public DamageExpression Damage { get; set; } = new();
}

/// <summary>Урон оружия: основная формула, урон по дальностям (дробовик) или радиус взрыва.</summary>
public sealed class DamageInfo
{
    public DamageExpression? Primary { get; set; }

    public List<RangeDamageEntry>? RangeDamages { get; set; }

    public int? BlastRadiusMeters { get; set; }

    public string RawText { get; set; } = string.Empty;

    /// <summary>false — числа не посчитать, показывается исходный текст и нужен ручной ввод.</summary>
    public bool IsParsed { get; set; }

    /// <summary>Формула для расчёта: основная, а у дробовика — ближняя дальность.</summary>
    public DamageExpression? GetDefaultDamage() => Primary ?? RangeDamages?.FirstOrDefault()?.Damage;

    /// <summary>Урон дроби (стр. 407): заряд разлетается, и урон зависит от дальности.</summary>
    public bool IsByRange => Primary is null && RangeDamages is { Count: > 0 };

    /// <summary>
    /// Формула на полосе дальности: у дробовика 0 — ближняя, 1 — средняя, 2 — большая (дальше последней — последняя, у обреза
    /// их две); у остального оружия — основная.
    /// </summary>
    public DamageExpression? At(int band) =>
        IsByRange ? RangeDamages![Math.Clamp(band, 0, RangeDamages.Count - 1)].Damage : GetDefaultDamage();

    public override string ToString() => RawText;
}
