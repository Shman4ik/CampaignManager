namespace CampaignManager.Web.Components.Features.Bestiary.Model;

public class CreatureCharacteristics
{
    /// <summary>
    ///     Сила
    /// </summary>
    public CreatureCharacteristicModel Strength { get; set; } = new();

    /// <summary>
    ///     Ловкость
    /// </summary>
    public CreatureCharacteristicModel Dexterity { get; set; } = new();

    /// <summary>
    ///     Интеллект
    /// </summary>
    public CreatureCharacteristicModel Intelligence { get; set; } = new();

    /// <summary>
    ///     Выносливость
    /// </summary>
    public CreatureCharacteristicModel Constitution { get; set; } = new();

    /// <summary>
    ///     Наружность
    /// </summary>
    public CreatureCharacteristicModel Appearance { get; set; } = new();

    /// <summary>
    ///     Мощь
    /// </summary>
    public CreatureCharacteristicModel Power { get; set; } = new();

    /// <summary>
    ///     Телосложение
    /// </summary>
    public CreatureCharacteristicModel Size { get; set; } = new();

    /// <summary>
    ///     Образование
    /// </summary>
    public CreatureCharacteristicModel Education { get; set; } = new();


    /// <summary>
    ///     Initiative value (ИН. В)
    /// </summary>
    public int Initiative { get; set; }

    /// <summary>
    ///     Average bonus to damage (Средний бонус к урону)
    /// </summary>
    public string AverageBonusToHit { get; set; } = string.Empty;

    /// <summary>
    ///     Average complexity/constitution rating (Средняя Комплекция)
    /// </summary>
    public int AverageComplexity { get; set; }

    /// <summary>
    ///     Movement speed (Скорость)
    /// </summary>
    public int Speed { get; set; }

    /// <summary>
    ///     ПЗ пункты здоровья
    /// </summary>
    public int HealPoint { get; set; }

    /// <summary>
    ///     ПМ пункты магии
    /// </summary>
    public int ManaPoint { get; set; }

    /// <summary>
    ///     Комплекция
    /// </summary>
    public int Constitutions { get; set; }
}