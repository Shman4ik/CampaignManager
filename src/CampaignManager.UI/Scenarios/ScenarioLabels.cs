using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Checks;
using CampaignManager.Core.Scenarios;
using CampaignManager.UI.Shared;

namespace CampaignManager.UI.Scenarios;

/// <summary>Подписи сценария в разметке: что проверяют, тон роли НПС. Сами слова — <see cref="ScenarioText"/> (Core).</summary>
public static class ScenarioLabels
{
    /// <summary>«Библиотеки», «СИЛ», «Удача» — то, что проверяют.</summary>
    public static string Target(ScenarioCheckDto check) => check.TargetKind switch
    {
        CheckTarget.Skill => check.SkillName ?? "навык удалён",
        CheckTarget.Characteristic when check.Characteristic is { } c => InvestigatorCreationRules.Info(c).Abbreviation,
        CheckTarget.Luck => "Удача",
        _ => ScenarioText.Of(check.TargetKind),
    };

    /// <summary>Сложность — только если не обычная: «обычная» на каждой строке ничего не говорит.</summary>
    public static string? Difficulty(ScenarioCheckDto check) =>
        check.Difficulty is Core.Difficulty.Regular ? null : CheckRules.DifficultyLabel(check.Difficulty);

    public static Tone RoleTone(NpcRole role) => role switch
    {
        NpcRole.Enemy => Tone.Error,
        NpcRole.Ally => Tone.Success,
        _ => Tone.Neutral,
    };

    /// <summary>Тон метки вида факта — один на вкладку «Факты» и панель режима игры.</summary>
    public static Tone FactTone(KeyFactType type) => type switch
    {
        KeyFactType.Truth => Tone.Error,
        KeyFactType.Timeline => Tone.Info,
        KeyFactType.Reward => Tone.Success,
        _ => Tone.Stone,
    };

    public static string CharacteristicName(Characteristic characteristic) =>
        InvestigatorCreationRules.Info(characteristic) is var info ? $"{info.Abbreviation} — {info.Name}" : characteristic.ToString();
}
