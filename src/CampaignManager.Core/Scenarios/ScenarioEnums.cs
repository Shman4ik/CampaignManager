namespace CampaignManager.Core.Scenarios;

/// <summary>Что проверяют в локации: навык, характеристику или Удачу.</summary>
public enum CheckTarget
{
    Skill,
    Characteristic,
    Luck,
}

public enum KeyFactType
{
    Backstory,
    Truth,
    Timeline,
    Reward,
}

/// <summary>Роль НПС в составе сценария.</summary>
public enum NpcRole
{
    Neutral,
    Enemy,
    Ally,
}
