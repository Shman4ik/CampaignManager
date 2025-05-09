﻿namespace CampaignManager.Web.Components.Features.Characters.Model;

public class Skill
{
    public string Name { get; set; }
    public AttributeValue Value { get; set; }
    public string BaseValue { get; set; }
    public bool IsUsed { get; set; } = false;
}