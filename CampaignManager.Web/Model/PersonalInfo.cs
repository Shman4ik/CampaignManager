namespace CampaignManager.Web.Model;

public class PersonalInfo
{
    /// <summary>
    ///     Имя игрока, управляющего персонажем
    /// </summary>
    public string PlayerName { get; set; } = "";

    /// <summary>
    ///     Имя персонажа
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Профессия или род занятий персонажа
    /// </summary>
    public string Occupation { get; set; } = "";

    /// <summary>
    ///     Возраст персонажа
    /// </summary>
    public int Age { get; set; } = 0;

    /// <summary>
    ///     Пол персонажа
    /// </summary>
    public string Gender { get; set; } = "";

    /// <summary>
    ///     Место рождения персонажа
    /// </summary>
    public string Birthplace { get; set; } = "";

    /// <summary>
    ///     Текущее место жительства персонажа
    /// </summary>
    public string Residence { get; set; } = "";

    /// <summary>
    ///     Бонус к урону персонажа
    /// </summary>
    public string DamageBonus { get; set; } = "0";

    /// <summary>
    ///     Скорость передвижения персонажа
    /// </summary>
    public int MoveSpeed { get; set; } = 0;

    /// <summary>
    ///     Значение уклонения персонажа
    /// </summary>
    public int Dodge { get; set; } = 0;

    /// <summary>
    ///     Телосложение персонажа
    /// </summary>
    public string Build { get; set; } = "0";
}