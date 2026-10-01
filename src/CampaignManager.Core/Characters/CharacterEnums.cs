namespace CampaignManager.Core.Characters;

/// <summary>Вид листа: сыщик игрока, преген или НПС. Кто владелец — решает вид (CHECK в базе).</summary>
public enum CharacterKind
{
    Player,
    Pregen,
    Npc,
}

public enum CharacterStatus
{
    Active,
    Inactive,
    Retired,
    Archived,
}
