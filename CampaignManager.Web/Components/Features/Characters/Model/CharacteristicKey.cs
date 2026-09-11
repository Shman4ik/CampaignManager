namespace CampaignManager.Web.Components.Features.Characters.Model;

/// <summary>
///     Восемь характеристик сыщика («Зов Ктулху» 7e, стр. 28–29).
///     Порядок перечисления — тот же, что на листе сыщика.
/// </summary>
public enum CharacteristicKey
{
    Strength,
    Constitution,
    Size,
    Dexterity,
    Appearance,
    Intelligence,
    Power,
    Education
}

/// <summary>Формула броска характеристики (стр. 28–29).</summary>
public enum CharacteristicDice
{
    /// <summary>3d6 × 5 — СИЛ, ВЫН, ЛВК, НАР, МОЩ.</summary>
    ThreeD6,

    /// <summary>(2d6 + 6) × 5 — ИНТ, ТЕЛ, ОБР.</summary>
    TwoD6Plus6
}

/// <summary>Справочная строка о характеристике: как её зовут и чем бросают.</summary>
public sealed record CharacteristicInfo(
    CharacteristicKey Key,
    string Abbreviation,
    string Name,
    CharacteristicDice Dice)
{
    public string DiceText => Dice is CharacteristicDice.ThreeD6 ? "3d6 × 5" : "(2d6 + 6) × 5";
}

/// <summary>
///     Строка таблицы возрастных модификаторов (стр. 30, памятка на стр. 32).
///     Штрафы к НАР и ОБР фиксированные, а вычет из физических характеристик игрок
///     распределяет сам — книга говорит «вычтите суммарно», а не «вычтите из каждой».
/// </summary>
public sealed record AgeBand(
    int MinAge,
    int MaxAge,
    string Name,
    int EducationChecks,
    int EducationPenalty,
    int AppearancePenalty,
    // 15–19 лет: суммарный вычет из СИЛ и/или ТЕЛ.
    int StrengthSizePenalty,
    // 40+ лет: суммарный вычет из СИЛ, ВЫН и/или ЛВК.
    int PhysicalPenalty,
    int LuckRolls,
    int MovePenalty)
{
    public string Range => $"{MinAge}–{MaxAge}";

    /// <summary>Характеристики, между которыми игрок распределяет вычет за возраст.</summary>
    public IReadOnlyList<CharacteristicKey> PenaltyTargets => StrengthSizePenalty > 0
        ? [CharacteristicKey.Strength, CharacteristicKey.Size]
        : PhysicalPenalty > 0
            ? [CharacteristicKey.Strength, CharacteristicKey.Constitution, CharacteristicKey.Dexterity]
            : [];

    /// <summary>Сколько всего пунктов нужно распределить между <see cref="PenaltyTargets" />.</summary>
    public int DistributedPenalty => StrengthSizePenalty > 0 ? StrengthSizePenalty : PhysicalPenalty;
}
