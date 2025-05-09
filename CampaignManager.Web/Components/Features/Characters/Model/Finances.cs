﻿namespace CampaignManager.Web.Components.Features.Characters.Model;

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
    ///     Карманные деньги
    /// </summary>
    public string PocketMoney { get; set; }
}