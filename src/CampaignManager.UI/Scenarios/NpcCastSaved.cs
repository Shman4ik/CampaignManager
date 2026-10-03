using CampaignManager.Core.Scenarios;

namespace CampaignManager.UI.Scenarios;

/// <summary>Что записало окно «Занять в сценарии»: сценарий (название — для тоста), роль и количество.</summary>
public sealed record NpcCastSaved(Guid ScenarioId, string ScenarioName, NpcRole Role, int Count);
