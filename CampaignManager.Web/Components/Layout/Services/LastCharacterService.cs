namespace CampaignManager.Web.Components.Layout.Services;

public sealed class LastCharacterService
{
    public string? CharacterId { get; private set; }
    public string? CharacterName { get; private set; }

    public event Action? OnChanged;

    public void Set(string characterId, string characterName)
    {
        CharacterId = characterId;
        CharacterName = characterName;
        OnChanged?.Invoke();
    }
}
