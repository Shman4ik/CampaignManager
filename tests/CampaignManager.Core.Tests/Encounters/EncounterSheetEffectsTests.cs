using CampaignManager.Core.Characters;
using CampaignManager.Core.Encounters;
using static CampaignManager.Core.Tests.Sheet.Sheets;

namespace CampaignManager.Core.Tests.Encounters;

/// <summary>
/// Итог сцены в листе (T2.6a): ПЗ, ПМ, рассудок с привыканием, МОЩ — правилами листа. В v1 итог боя в лист не
/// попадал: рассудок и привыкание правили оторванную копию (AUDIT, «Сцены»).
/// </summary>
public sealed class EncounterSheetEffectsTests
{
    private static CharacterSheet Harvey()
    {
        var sheet = NewSheet(50);
        sheet.Current.HitPoints = 14;
        sheet.Current.MagicPoints = 14;
        return sheet;
    }

    private static EncounterEffect Effect(EncounterEffectKind kind, int amount) => new() { Kind = kind, Amount = amount };

    [Fact]
    [Trait("page", "117")]
    public void Damage_uses_wound_rules_of_the_sheet()
    {
        var sheet = Harvey();

        var lines = EncounterSheetEffects.Apply(sheet, Catalog, [Effect(EncounterEffectKind.Damage, 3), Effect(EncounterEffectKind.Damage, 8)]);

        Assert.Equal(3, sheet.Current.HitPoints);
        Assert.True(sheet.Condition.MajorWound);
        Assert.Equal(["ПЗ 14 → 11", "ПЗ 11 → 3, серьёзная рана"], lines);
    }

    [Fact]
    public void Heal_is_capped_and_restores_consciousness()
    {
        var sheet = Harvey();
        EncounterSheetEffects.Apply(sheet, Catalog, [Effect(EncounterEffectKind.Damage, 6), Effect(EncounterEffectKind.Damage, 6), Effect(EncounterEffectKind.Damage, 6)]);
        Assert.True(sheet.Condition.Unconscious);

        EncounterSheetEffects.Apply(sheet, Catalog, [Effect(EncounterEffectKind.Heal, 99)]);

        Assert.Equal(14, sheet.Current.HitPoints);
        Assert.False(sheet.Condition.Unconscious);
    }

    [Fact]
    public void Magic_points_are_clamped_and_power_is_permanent()
    {
        var sheet = Harvey();

        EncounterSheetEffects.Apply(sheet, Catalog, [Effect(EncounterEffectKind.MagicPoints, -20)]);
        Assert.Equal(0, sheet.Current.MagicPoints);

        sheet.Current.MagicPoints = 14;
        var lines = EncounterSheetEffects.Apply(sheet, Catalog, [Effect(EncounterEffectKind.Power, -10)]);

        Assert.Equal(60, sheet.Characteristics.Pow);
        Assert.Equal(12, sheet.Current.MagicPoints); // максимум ПМ = МОЩ / 5 (стр. 31)
        Assert.Equal("МОЩ 70 → 60", lines[0]);
    }

    [Fact]
    [Trait("page", "153")]
    public void Sanity_loss_is_one_cause_and_prompts_int_check()
    {
        var sheet = Harvey();

        var lines = EncounterSheetEffects.Apply(sheet, Catalog, [Effect(EncounterEffectKind.SanityLoss, 6)]);

        Assert.Equal(44, sheet.Current.Sanity);
        Assert.Equal(6, sheet.Condition.LastSanityLoss);
        Assert.Contains("проверка ИНТ", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    [Trait("page", "167")]
    public void Creature_sanity_loss_goes_through_habituation_by_kind()
    {
        var sheet = Harvey();
        var creatureId = Guid.CreateVersion7();
        EncounterEffect FromDeepOne(int amount) => new()
        {
            Kind = EncounterEffectKind.SanityLoss,
            Amount = amount,
            CreatureName = "Глубоководный",
            CreatureId = creatureId,
            SanityLossFormula = "0/1D6",
        };

        EncounterSheetEffects.Apply(sheet, Catalog, [FromDeepOne(4)]);
        var lines = EncounterSheetEffects.Apply(sheet, Catalog, [FromDeepOne(5)]);

        var habituation = Assert.Single(sheet.Condition.Habituations); // вид — один счётчик
        Assert.Equal(6, habituation.MaxLoss);
        Assert.Equal(6, habituation.LostSanity);
        Assert.Equal(44, sheet.Current.Sanity); // 4 + только 2 из 5: предел вида — 6
        Assert.Contains("потеряно 2 из 5", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Out_effect_does_not_touch_the_sheet()
    {
        Assert.False(EncounterSheetEffects.TouchesSheet(EncounterEffectKind.Out));
        Assert.True(EncounterSheetEffects.TouchesSheet(EncounterEffectKind.SanityLoss));
    }
}
