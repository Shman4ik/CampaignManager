namespace CampaignManager.Web.Components.Features.Characters.Model;

public class DerivedAttributes
{
    /// <summary>
    ///     Очки здоровья персонажа
    /// </summary>
    public AttributeWithMaxValue HitPoints { get; set; } = new(0, 0);

    /// <summary>
    ///     Очки магии персонажа
    /// </summary>
    public AttributeWithMaxValue MagicPoints { get; set; } = new(0, 0);

    /// <summary>
    ///     Уровень рассудка персонажа
    /// </summary>
    public AttributeWithMaxValue Sanity { get; set; } = new(0, 99);

    /// <summary>
    ///     Уровень удачи персонажа
    /// </summary>
    public AttributeWithMaxValue Luck { get; set; } = new(0, 99);
}