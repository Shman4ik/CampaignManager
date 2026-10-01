namespace CampaignManager.Core.Campaigns;

public enum CampaignKind
{
    Campaign,
    OneShot,
}

public enum CampaignStatus
{
    Planning,
    Active,
    OnHold,
    Completed,
}

/// <summary>Роль участника кампании. Хранитель — тоже участник, ровно один на кампанию.</summary>
public enum CampaignRole
{
    Player,
    Keeper,
}

/// <summary>Прохождение сценария в кампании.</summary>
public enum ScenarioRunStatus
{
    Planned,
    Announced,
    Running,
    Finished,
}
