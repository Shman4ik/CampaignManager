namespace CampaignManager.Core.Encounters;

/// <summary>Сцена за столом: бой или погоня — одна таблица, одно ядро.</summary>
public enum EncounterKind
{
    Combat,
    Chase,
}

public enum EncounterStatus
{
    Active,
    Finished,
}
