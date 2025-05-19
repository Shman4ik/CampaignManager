namespace CampaignManager.Web.Components.Features.Characters.Model;

public class Finances
{
    /// <summary>
    ///     Наличные средства (в валюте игрового мира).
    /// </summary>
    public string Cash { get; set; } = string.Empty;

    /// <summary>
    ///     Активы персонажа
    /// </summary>
    public List<string> Assets { get; set; } = [];

    /// <summary>
    ///     Карманные деньги (в валюте игрового мира).
    /// </summary>
    public string PocketMoney { get; set; } = string.Empty;
}