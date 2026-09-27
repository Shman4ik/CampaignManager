using CampaignManager.Web.Components.Features.Characters.Services;
using CampaignManager.Web.Components.Features.Checks.Model;
using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Web.Components.Features.Checks.Services;

/// <summary>Итог одного броска проверки: что выпало, какой это уровень и пройдена ли сложность.</summary>
public sealed record CheckOutcome(int Roll, SuccessLevel Level, bool Passed);

/// <summary>
///     Проверка навыка или характеристики вне боя (гл. 5, стр. 80–97): сложность, бонусные и штрафные
///     кости, повторная проверка, трата Удачи и отметка для фазы развития.
///     <para>
///         Сам уровень успеха здесь <b>не считается</b> — только <see cref="CombatService.CalculateSuccessLevel(int, int, SuccessLevel)" />,
///         единственная копия порогов в приложении, а трата Удачи — только <see cref="LuckRules" />.
///         Этот класс решает то, чего нет ни там, ни там: что разрешено после броска.
///     </para>
/// </summary>
public static class SkillCheckRules
{
    /// <summary>Больше двух бонусных или штрафных костей книга не даёт (стр. 89).</summary>
    public const int MaxExtraDice = 2;

    /// <summary>Три уровня сложности проверки навыка (стр. 80).</summary>
    public static readonly IReadOnlyList<SuccessLevel> Difficulties =
        [SuccessLevel.RegularSuccess, SuccessLevel.HardSuccess, SuccessLevel.ExtremeSuccess];

    /// <summary>Сколько нужно выбросить при этой сложности.</summary>
    public static int TargetNumber(int value, SuccessLevel difficulty) =>
        CombatService.GetTargetNumber(value, difficulty);

    /// <summary>
    ///     Бросок d100 с бонусными (<paramref name="netDice" /> &gt; 0) или штрафными (&lt; 0) костями —
    ///     кости уже погашены друг другом, как в диалоге (стр. 89).
    /// </summary>
    public static DiceRollResult Roll(int netDice)
    {
        var net = Math.Clamp(netDice, -MaxExtraDice, MaxExtraDice);
        return CombatService.RollD100(Math.Max(0, net), Math.Max(0, -net));
    }

    public static CheckOutcome Evaluate(int roll, int value, SuccessLevel difficulty)
    {
        var level = CombatService.CalculateSuccessLevel(roll, value, difficulty);
        return new CheckOutcome(roll, level, level >= difficulty);
    }

    /// <summary>
    ///     Почему провал нельзя повторить (стр. 82–84, 87, 102); <c>null</c> — можно.
    ///     Повторяют только навыки и характеристики, один раз и не после краха.
    /// </summary>
    public static string? PushBlockReason(CheckTarget target, CheckOutcome first, bool inCombat)
    {
        if (first.Passed)
            return "Проверка пройдена — повторять нечего.";
        if (inCombat)
            return "В бою повторных проверок не бывает: новая попытка — это действие в следующем раунде (стр. 102).";
        if (target.Kind is CheckTargetKind.Luck)
            return "Проверку Удачи повторить нельзя (стр. 83).";
        if (target.Kind is CheckTargetKind.Skill && IsCombatSkill(target.Name))
            return "Ближний бой и Стрельбу повторно не проверяют — следующая атака и есть вторая попытка (стр. 102).";
        if (first.Level is SuccessLevel.Fumble)
            return "Крах наступает сразу, повторной проверкой его не отменить (стр. 87).";
        return null;
    }

    /// <summary>
    ///     Почему за этот бросок нельзя заплатить Удачей (стр. 97); <c>null</c> — можно.
    ///     Крах здесь считается с учётом сложности, а не так, как видит его <see cref="LuckRules" />:
    ///     97 на трудной проверке навыка 60 — уже крах.
    /// </summary>
    public static string? LuckBlockReason(CheckTarget target, CheckOutcome outcome, bool isPushed)
    {
        if (outcome.Passed)
            return null;
        if (target.Kind is CheckTargetKind.Luck)
            return "На проверку Удачи пункты Удачи не тратят (стр. 97).";
        if (isPushed)
            return "На повторную проверку Удачу не тратят: либо повтор, либо Удача (стр. 97).";
        if (outcome.Level is SuccessLevel.Fumble or SuccessLevel.CriticalSuccess)
            return "Крах вступает в силу в любом случае — выкупить его нельзя (стр. 97).";
        return null;
    }

    /// <summary>
    ///     Во что обойдётся дотянуть провал до нужной сложности и выше. Цены считает <see cref="LuckRules" />;
    ///     уровни ниже сложности отброшены — за них платить бессмысленно.
    /// </summary>
    public static IReadOnlyList<LuckRules.SpendOption> LuckOptions(
        CheckOutcome outcome, int value, SuccessLevel difficulty, int currentLuck) =>
        LuckRules.Options(outcome.Roll, value, currentLuck)
            .Where(option => option.Level >= difficulty)
            .ToList();

    /// <summary>
    ///     Почему успех не даёт отметки для фазы развития (стр. 92, 97); <c>null</c> — даёт.
    ///     Отмечают только навыки, и не Мифы Ктулху со Средствами.
    /// </summary>
    public static string? MarkBlockReason(CheckTarget target, int netDice, bool luckSpent)
    {
        if (target.Kind is CheckTargetKind.Characteristic or CheckTargetKind.Luck)
            return "Отметку для развития ставят только навыкам, не характеристикам и не Удаче (стр. 92).";
        if (target.Kind is CheckTargetKind.Skill && !DevelopmentPhaseRules.CanBeChecked(target.Name))
            return "Мифы Ктулху и Средства никогда не отмечают (стр. 92).";
        if (netDice > 0)
            return "Проверка шла с бонусной костью — навык не отмечают (стр. 92).";
        if (luckSpent)
            return "Успех куплен Удачей — отметки за него нет (стр. 97).";
        if (target.Kind is CheckTargetKind.Other)
            return "Значение вписано вручную: если это был навык сыщика, отметку ставят на его листе (стр. 92).";
        return null;
    }

    /// <summary>Ближний бой и Стрельба — проверки, которые книга запрещает повторять (стр. 102).</summary>
    public static bool IsCombatSkill(string skillName) =>
        skillName.StartsWith("Ближний бой", StringComparison.OrdinalIgnoreCase)
        || skillName.StartsWith("Стрельба", StringComparison.OrdinalIgnoreCase);

    /// <summary>Сложность проверки из локации сценария: там она хранится строкой «Hard»/«Extreme».</summary>
    public static SuccessLevel ParseScenarioDifficulty(string? difficulty) => difficulty switch
    {
        "Hard" => SuccessLevel.HardSuccess,
        "Extreme" => SuccessLevel.ExtremeSuccess,
        _ => SuccessLevel.RegularSuccess
    };

    /// <summary>Сложность в подписи кнопки: «обычная», «трудная», «чрезвычайная» (стр. 80).</summary>
    public static string DifficultyLabel(SuccessLevel difficulty) => difficulty switch
    {
        SuccessLevel.HardSuccess => "Трудная",
        SuccessLevel.ExtremeSuccess => "Чрезвычайная",
        _ => "Обычная"
    };

    /// <summary>Что означает число костей в переключателе.</summary>
    public static string ExtraDiceLabel(int netDice) => netDice switch
    {
        >= 2 => "две бонусные кости",
        1 => "одна бонусная кость",
        -1 => "одна штрафная кость",
        <= -2 => "две штрафные кости",
        _ => "без дополнительных костей"
    };

    /// <summary>Название уровня с заглавной буквы — для крупной подписи результата.</summary>
    public static string LevelTitle(SuccessLevel level)
    {
        var text = CombatService.GetSuccessLevelText(level);
        return text.Length == 0 ? text : char.ToUpper(text[0]) + text[1..];
    }
}
