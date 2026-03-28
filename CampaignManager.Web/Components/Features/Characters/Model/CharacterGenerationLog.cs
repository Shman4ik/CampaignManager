namespace CampaignManager.Web.Components.Features.Characters.Model;

/// <summary>
/// Одна запись лога генерации персонажа
/// </summary>
public sealed class GenerationLogEntry
{
    public required string Category { get; init; }
    public required string Description { get; init; }
    public string? DiceRoll { get; init; }
    public int? Result { get; init; }
    public string? Details { get; init; }
}

/// <summary>
/// Полный лог генерации персонажа — хранит все шаги
/// </summary>
public sealed class CharacterGenerationLog
{
    public List<GenerationLogEntry> Entries { get; } = [];

    public void Add(string category, string description, string? diceRoll = null, int? result = null, string? details = null)
    {
        Entries.Add(new GenerationLogEntry
        {
            Category = category,
            Description = description,
            DiceRoll = diceRoll,
            Result = result,
            Details = details
        });
    }
}
