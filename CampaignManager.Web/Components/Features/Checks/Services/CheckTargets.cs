using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Characters.Services;
using CampaignManager.Web.Components.Features.Checks.Model;
using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Web.Components.Features.Checks.Services;

/// <summary>
///     Цели проверки, снятые с листа сыщика: восемь характеристик, Удача и навыки. Рассудка здесь
///     нет намеренно — у его проверки свои правила и своя панель (<c>Characters/Components/SanityPanel</c>).
/// </summary>
public static class CheckTargets
{
    public const string LuckName = "Удача";

    public static IReadOnlyList<CheckTarget> Characteristics(Character character) =>
        InvestigatorCreationRules.Characteristics
            .Select(info => new CheckTarget(
                CheckTargetKind.Characteristic,
                info.Abbreviation,
                InvestigatorFactory.Read(character.Characteristics, info.Key)))
            .ToList();

    public static CheckTarget Luck(Character character) =>
        new(CheckTargetKind.Luck, LuckName, character.DerivedAttributes.Luck.Value);

    public static CheckTarget ForCharacteristic(Character character, CharacteristicKey key) =>
        new(CheckTargetKind.Characteristic,
            InvestigatorCreationRules.Info(key).Abbreviation,
            InvestigatorFactory.Read(character.Characteristics, key));

    public static CheckTarget ForSkill(Skill skill) =>
        new(CheckTargetKind.Skill, skill.Name, skill.Value.Regular);

    /// <summary>Навык листа по имени — ровно как он записан, без эвристик: его отмечают галочкой.</summary>
    public static Skill? FindSkill(Character character, string name) =>
        character.Skills.SkillGroups
            .SelectMany(g => g.Skills)
            .FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal));

    /// <summary>Цель по ключу выпадающего списка (<see cref="CheckTarget.Key" />).</summary>
    public static CheckTarget? FindByKey(Character character, string key)
    {
        if (key == CheckTarget.KeyFor(CheckTargetKind.Luck, LuckName))
            return Luck(character);

        if (key.StartsWith("char:", StringComparison.Ordinal))
            return Characteristics(character).FirstOrDefault(t => t.Key == key);

        if (key.StartsWith("skill:", StringComparison.Ordinal))
            return FindSkill(character, key["skill:".Length..]) is { } skill ? ForSkill(skill) : null;

        return null;
    }

    /// <summary>
    ///     Цель по имени, как её пишут в проверке локации сценария: «СИЛ» или «Сила», «Удача»,
    ///     иначе навык. Навык ищет <see cref="CombatService.FindSkillValue(Character, string)" /> —
    ///     тот же разбор, что у боя: «Внимание» и «Стрельба (П)» находятся и в сокращённом виде.
    /// </summary>
    public static CheckTarget ResolveByName(Character character, string name)
    {
        var trimmed = name.Trim();

        if (string.Equals(trimmed, LuckName, StringComparison.OrdinalIgnoreCase))
            return Luck(character);

        var characteristic = InvestigatorCreationRules.Characteristics.FirstOrDefault(info =>
            string.Equals(info.Abbreviation, trimmed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(info.Name, trimmed, StringComparison.OrdinalIgnoreCase));

        if (characteristic is not null)
            return ForCharacteristic(character, characteristic.Key);

        var exact = FindSkill(character, trimmed);
        return exact is not null
            ? ForSkill(exact)
            : new CheckTarget(CheckTargetKind.Skill, trimmed, CombatService.FindSkillValue(character, trimmed));
    }
}
