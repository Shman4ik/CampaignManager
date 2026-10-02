using CampaignManager.Core.Characters;

namespace CampaignManager.UI.Checks;

/// <summary>
/// Сыщик для выбора в проверке вне его листа (режим игры сценария, групповая проверка): id листа, имя,
/// игрок и сам лист — только для чтения. Состав кампании собирает страница из API (T2.2/T2.3).
/// </summary>
public sealed record CheckInvestigator(Guid Id, string Name, CharacterSheet Sheet, string? PlayerName = null)
{
    /// <summary>«Харви Уолтерс (Аня)».</summary>
    public string Label => string.IsNullOrWhiteSpace(PlayerName) ? Name : $"{Name} ({PlayerName})";
}
