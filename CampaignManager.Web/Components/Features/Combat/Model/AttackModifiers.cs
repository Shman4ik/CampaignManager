namespace CampaignManager.Web.Components.Features.Combat.Model;

/// <summary>
/// Бонусные и штрафные кости, применяемые к проверке атаки, вместе с их обоснованием.
/// Кости здесь ещё не погашены взаимно — это делает бросок.
/// </summary>
/// <param name="BonusDice">Общее число бонусных костей.</param>
/// <param name="PenaltyDice">Общее число штрафных костей.</param>
/// <param name="Reasons">Расшифровка для Хранителя, по одной строке на модификатор.</param>
public sealed record AttackModifiers(int BonusDice, int PenaltyDice, IReadOnlyList<string> Reasons)
{
    /// <summary>Нетто после взаимного погашения: положительное — бонус, отрицательное — штраф.</summary>
    public int Net => BonusDice - PenaltyDice;

    public static AttackModifiers None { get; } = new(0, 0, []);
}
