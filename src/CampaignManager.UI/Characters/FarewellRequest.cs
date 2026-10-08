using CampaignManager.Contracts.Campaigns;

namespace CampaignManager.UI.Characters;

/// <summary>Что решили в «Проводить сыщика»: эпилог (пусто — без него) и встреча журнала, куда его дописать (null — не дописывать).</summary>
public sealed record FarewellRequest(string Epilogue, CampaignSessionDto? Session);
