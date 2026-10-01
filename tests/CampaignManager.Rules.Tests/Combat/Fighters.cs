using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Rules.Tests.Combat;

/// <summary>Участники и бой для тестов: только числа, без листов и базы.</summary>
internal static class Fighters
{
    public static Combatant Make(string name, int hp = 12, CombatSide side = CombatSide.Party) => new()
    {
        Name = name,
        MaxHitPoints = hp,
        CurrentHitPoints = hp,
        ConstitutionValue = 50,
        IntelligenceValue = 60,
        MaxSanity = 99,
        CurrentSanity = 50,
        Side = side
    };

    /// <summary>Участник с листом сыщика — у него есть рассудок и привыкание.</summary>
    public static Combatant Investigator(string name, int sanity = 50, int intelligence = 60)
    {
        var sheet = new Character();
        sheet.PersonalInfo.Name = name;
        sheet.DerivedAttributes.Sanity = new AttributeWithMaxValue(sanity, 99);

        var combatant = Make(name);
        combatant.CharacterSource = sheet;
        combatant.CurrentSanity = sanity;
        combatant.IntelligenceValue = intelligence;
        return combatant;
    }

    public static CombatService Battle(params Combatant[] combatants)
    {
        var combat = new CombatService();
        foreach (var combatant in combatants)
            combat.AddCombatant(combatant);
        return combat;
    }
}
