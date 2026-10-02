namespace CampaignManager.Core.Dice;

/// <summary>
/// Уровень успеха броска (стр. 86–89) — что выпало. Отдельно от <see cref="Difficulty"/> — что
/// требовалось: в v1 одно перечисление служило обоим, и сложность «провал» имела смысл «весь навык».
/// Порядок членов значим: больше — лучше.
/// </summary>
public enum SuccessLevel
{
    Fumble,
    Failure,
    Regular,
    Hard,
    Extreme,
    Critical,
}
