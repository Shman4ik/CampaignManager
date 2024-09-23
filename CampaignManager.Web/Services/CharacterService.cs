using CampaignManager.Web.Model;

namespace CampaignManager.Web.Services;

public class CharacterService
{
    private readonly List<Character> _characters = new();

    public async Task<Character> CreateCharacterAsync(Character character)
    {
        character.Id = Guid.NewGuid();
        _characters.Add(character);
        return await Task.FromResult(character);
    }

    public async Task<Character> GetCharacterByIdAsync(Guid id)
    {
        var character = _characters.FirstOrDefault(c => c.Id == id);
        return await Task.FromResult(character);
    }

    public async Task<IEnumerable<Character>> GetAllCharactersAsync()
    {
        return await Task.FromResult(_characters);
    }

    public async Task<Character> UpdateCharacterAsync(Character character)
    {
        var existingCharacter = _characters.FirstOrDefault(c => c.Id == character.Id);
        if (existingCharacter != null)
        {
            _characters.Remove(existingCharacter);
            _characters.Add(character);
        }
        return await Task.FromResult(character);
    }

    public async Task<bool> DeleteCharacterAsync(Guid id)
    {
        var character = _characters.FirstOrDefault(c => c.Id == id);
        if (character != null)
        {
            _characters.Remove(character);
            return await Task.FromResult(true);
        }
        return await Task.FromResult(false);
    }
}