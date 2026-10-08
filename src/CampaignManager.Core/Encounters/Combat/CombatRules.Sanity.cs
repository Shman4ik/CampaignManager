using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Encounters;

/// <summary>Проверка Рассудка в сцене (гл. 8). Потеря — «успех/провал»; тварь — источник привыкания.</summary>
public sealed record SanityCheckSetup
{
    public Guid TargetId { get; init; }

    /// <summary>Тварь сцены — источник: её запись потери и вид для привыкания (стр. 167).</summary>
    public Guid? CreatureParticipantId { get; init; }

    public string SuccessLoss { get; init; } = "0";
    public string FailureLoss { get; init; } = "1D6";

    public D100Roll? Roll { get; init; }

    /// <summary>Выпавшая потеря, если её бросили на столе; null — бросит правило (при крахе — максимум).</summary>
    public int? LossRoll { get; init; }

    /// <summary>ИНТ при потере 5+ (стр. 153).</summary>
    public D100Roll? IntRoll { get; init; }
}

public static partial class CombatRules
{
    /// <summary>
    /// Проверка Рассудка (стр. 152–154): бросок против текущего Рассудка без бонусных и штрафных костей; успех — потеря
    /// успеха, провал — провала, крах — максимум провальной части. Потеря 5+ — проверка ИНТ: <b>успех</b> — сыщик осознал
    /// ужас, временное безумие (исход едет в эффекте, лист решает сам — после привыкания потеря могла стать меньше 5).
    /// От твари — привыкание к её виду (стр. 167) считает лист. Пороги ⅕ за день и ноль показывает лист (<c>SanityRules.Status</c>).
    /// </summary>
    public static EncounterResolution SanityCheck(EncounterState state, SanityCheckSetup setup, IDiceRoller dice)
    {
        var target = Require(state, setup.TargetId);
        if (target.Sanity is not { } sanity)
            return Resolution(EncounterLogKind.Sanity, target.Id, $"У {target.Name} нет рассудка — проверка не нужна.", [], []);

        var creature = setup.CreatureParticipantId is { } creatureId ? state.Find(creatureId) : null;
        var roll = setup.Roll ?? D100.Roll(dice);
        var level = Check.Evaluate(roll.Result, sanity);
        var (formula, fumble) = SanityRules.LossFor(level, setup.SuccessLoss, setup.FailureLoss);
        var loss = Math.Max(0, fumble ? formula.Max : setup.LossRoll ?? formula.Roll(dice));

        List<string> lines =
        [
            RollText($"{target.Name} (Рассудок)", roll, sanity, level),
            fumble ? $"Крах — максимальная потеря: {formula.Text} → {N(loss)} (стр. 153)." : $"Потеря: {formula.Text} → {N(loss)}.",
        ];

        bool? intPassed = null;
        if (loss >= 5)
        {
            var (intRoll, intLevel, passed) = Test(setup.IntRoll, target.Stats.Int, dice);
            intPassed = passed;
            lines.Add($"Потеряно 5+ за раз — {RollText("ИНТ", intRoll, target.Stats.Int, intLevel)}: " +
                      (passed ? "сыщик осознал ужас — временное безумие и приступ (стр. 153–154)." : "разум отгородился от ужаса, безумия нет."));
        }

        if (creature is not null && target.HasSheet)
            lines.Add($"Привыкание к виду «{creature.SourceName}» учтёт лист: за предел этот вид рассудка не отнимает (стр. 167).");

        var after = Math.Max(0, sanity - loss);
        if (after == 0)
            lines.Add($"{target.Name} теряет рассудок полностью — неизлечимое безумие (стр. 154).");

        var effect = new EncounterEffect
        {
            Kind = EncounterEffectKind.SanityLoss,
            ParticipantId = target.Id,
            Amount = loss,
            Check = intPassed,
            Detail = $"{RulesText.Of(level)}, {formula.Text}",
            CreatureName = creature?.SourceName,
            CreatureId = creature?.SourceCreatureId,
            SanityLossFormula = creature?.SanityLoss,
        };
        return Resolution(EncounterLogKind.Sanity, target.Id,
            $"{target.Name}: проверка Рассудка{(creature is null ? "" : $" — {creature.SourceName}")} — {RulesText.Of(level)}, потеря {N(loss)}.",
            lines, loss > 0 || intPassed is not null ? [effect] : []);
    }

    /// <summary>Запись потери «успех/провал» твари по частям — подставить в форму.</summary>
    public static (string Success, string Failure) SanityLossParts(string? record)
    {
        var parts = SanityLossFormula.Parse(record);
        return (parts.OnSuccess.Text.Length == 0 ? "0" : parts.OnSuccess.Text, parts.OnFailure.Text.Length == 0 ? "0" : parts.OnFailure.Text);
    }
}
