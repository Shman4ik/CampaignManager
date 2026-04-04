namespace CampaignManager.Web.Model;

/// <summary>
/// Одна кость в формуле урона (например, 2d6, d4, -1d3).
/// </summary>
public sealed record DiceTerm(int Count, int Sides, bool IsNegative = false)
{
    /// <summary>Максимальное значение броска этого терма.</summary>
    public int MaxValue => IsNegative ? -(Count * Sides) : Count * Sides;

    public override string ToString() =>
        IsNegative ? $"-{Count}d{Sides}" : $"{Count}d{Sides}";
}

/// <summary>
/// Тип бонуса к урону (Б.К.У.) по правилам CoC 7e.
/// </summary>
public enum DamageBonusType
{
    /// <summary>Б.К.У. не применяется (огнестрельное оружие).</summary>
    None,

    /// <summary>Полный Б.К.У. (большинство оружия ближнего боя).</summary>
    Full,

    /// <summary>Половина Б.К.У. (метательное оружие, хлыст и т.п.).</summary>
    Half
}

/// <summary>
/// Универсальная структурированная формула урона по правилам CoC 7e.
/// Используется для оружия, заклинаний, ловушек и других источников урона.
/// Хранится в БД как JSONB (когда используется в сущностях с JSONB).
/// </summary>
public sealed class DamageExpression
{
    /// <summary>Список костей в формуле (например, [2d6, 1d4]).</summary>
    public List<DiceTerm> Dice { get; set; } = [];

    /// <summary>Плоский числовой модификатор (например, +2, -1).</summary>
    public int FlatModifier { get; set; }

    /// <summary>Тип бонуса к урону.</summary>
    public DamageBonusType DamageBonus { get; set; } = DamageBonusType.None;

    /// <summary>
    /// Список специальных эффектов (например, ["горение"], ["шок"]).
    /// Текстовые эффекты, которые не влияют на расчёт HP-урона напрямую.
    /// </summary>
    public List<string> Effects { get; set; } = [];

    /// <summary>
    /// Оригинальная текстовая строка урона до парсинга.
    /// Всегда сохраняется для отображения и fallback.
    /// </summary>
    public string RawText { get; set; } = string.Empty;

    /// <summary>
    /// Был ли оригинальный текст успешно распарсен.
    /// False — значение нельзя автоматически рассчитать; необходим ввод вручную.
    /// </summary>
    public bool IsParsed { get; set; }

    /// <returns>Человекочитаемая строка формулы (например, "2d6 + 1 + Б.К.У.").</returns>
    public override string ToString()
    {
        if (!IsParsed)
            return RawText;

        var parts = new List<string>();

        foreach (var die in Dice)
            parts.Add(die.ToString());

        if (FlatModifier != 0)
            parts.Add(FlatModifier > 0 ? $"+{FlatModifier}" : FlatModifier.ToString());

        if (DamageBonus == DamageBonusType.Full)
            parts.Add("+ Б.К.У.");
        else if (DamageBonus == DamageBonusType.Half)
            parts.Add("+ ½ Б.К.У.");

        foreach (var effect in Effects)
            parts.Add($"+ {effect}");

        return parts.Count == 0 ? "0" : string.Join(" ", parts).TrimStart('+', ' ');
    }
}
