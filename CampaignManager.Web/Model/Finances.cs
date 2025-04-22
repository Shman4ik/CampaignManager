namespace CampaignManager.Web.Model;

public class Finances
{
    /// <summary>
    ///     Наличные деньги персонажа
    /// </summary>
    public string Cash { get; set; }

    /// <summary>
    ///     Активы персонажа
    /// </summary>
    public List<string> Assets { get; set; } = [];

    /// <summary>
    ///     Ключевые точки персонажа
    /// </summary>
    public string PocketMoney { get; set; }
}