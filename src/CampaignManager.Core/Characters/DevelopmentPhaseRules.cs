using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Characters;

/// <summary>
/// Фаза развития сыщиков: проверки опыта и Средства (стр. 92–94), восстановление Удачи (стр. 93),
/// награда Хранителя, самолечение и привыкание к ужасному (стр. 164–167). Единственное место этих
/// формул: окно фазы только показывает результат. Любой бросок можно вписать — для этого правила
/// принимают <see cref="IDiceRoller"/>, а окно подставляет вписанное.
/// </summary>
public static class DevelopmentPhaseRules
{
    /// <summary>Навык, доросший до 90%, даёт +2d6 Рассудка (стр. 92).</summary>
    public const int MasteryThreshold = 90;

    /// <summary>Выше 95 навык растёт, даже если и так высок (стр. 92).</summary>
    public const int AlwaysImprovesAbove = 95;

    /// <summary>Прирост навыка при успешной проверке опыта (стр. 92).</summary>
    public static readonly DiceSpec SkillGain = new(1, 10);

    /// <summary>Рассудок за навык, впервые дошедший до 90% (стр. 92).</summary>
    public static readonly DiceSpec MasterySanity = new(2, 6);

    /// <summary>Прирост Удачи при восстановлении (стр. 93).</summary>
    public static readonly DiceSpec LuckGain = new(1, 10);

    /// <summary>Рассудок при успешном самолечении (стр. 165).</summary>
    public static readonly DiceSpec SelfHealingGain = new(1, 6);

    /// <summary>Кости варианта «занятия и Средства» (стр. 94); null — бросать нечего («Жизнь идёт своим чередом»).</summary>
    public static DiceSpec? CreditRatingDice(CreditRatingChange change) => change switch
    {
        CreditRatingChange.Rich => new DiceSpec(1, 10),
        CreditRatingChange.Promotion => new DiceSpec(1, 6),
        CreditRatingChange.TightenBelt or CreditRatingChange.SoldSilver => new DiceSpec(1, 10),
        CreditRatingChange.RoughPatch => new DiceSpec(2, 10),
        CreditRatingChange.Bankrupt => new DiceSpec(1, 100),
        _ => null,
    };

    /// <summary>Мифы Ктулху и Средства не отмечают — «у этих навыков даже нет места для галочки» (стр. 92).</summary>
    public static bool CanBeChecked(string? skillCode) =>
        skillCode is not (SkillCodes.Mythos or SkillCodes.CreditRating);

    public static bool CanBeChecked(SheetSkill skill, SkillCatalog catalog) => CanBeChecked(skill.CodeOf(catalog));

    /// <summary>Отмеченные навыки для проверок опыта, по имени. Окно снимает этот список один раз на открытие.</summary>
    public static List<SheetSkill> CheckedSkills(CharacterSheet sheet, SkillCatalog catalog) =>
        sheet.Skills
            .Where(s => s.Checked && CanBeChecked(s, catalog))
            .OrderBy(s => s.DisplayName(catalog), StringComparer.CurrentCulture)
            .ToList();

    /// <summary>
    /// Проверка опыта: 1d100 больше значения (или выше 95) — навык растёт на 1d10, иначе нет (стр. 92).
    /// Значение может превысить 100%. Впервые дошёл до 90% — +2d6 Рассудка.
    /// </summary>
    public static SkillImprovementResult RollSkillImprovement(
        CharacterSheet sheet, SkillCatalog catalog, SheetSkill skill, IDiceRoller dice)
    {
        var name = skill.DisplayName(catalog);
        var oldValue = skill.Value;
        var roll = dice.Percentile();

        if (roll <= oldValue && roll <= AlwaysImprovesAbove)
            return new SkillImprovementResult { SkillName = name, OldValue = oldValue, Roll = roll, NewValue = oldValue };

        var gain = dice.Roll(SkillGain.Count, SkillGain.Sides);
        skill.Value = oldValue + gain;

        var reachedMastery = oldValue < MasteryThreshold && skill.Value >= MasteryThreshold;
        var sanityGain = reachedMastery ? SanityRules.Grant(sheet, catalog, dice.Roll(MasterySanity.Count, MasterySanity.Sides)) : 0;

        return new SkillImprovementResult
        {
            SkillName = name,
            OldValue = oldValue,
            Roll = roll,
            Improved = true,
            Gain = gain,
            NewValue = skill.Value,
            ReachedMastery = reachedMastery,
            SanityGain = sanityGain,
        };
    }

    /// <summary>Стирает все отметки — последний шаг фазы (стр. 92).</summary>
    public static void ClearSkillChecks(CharacterSheet sheet)
    {
        foreach (var skill in sheet.Skills)
            skill.Checked = false;
    }

    /// <summary>Восстановление Удачи (стр. 93): 1d100 больше текущей — +1d10, не выше 99.</summary>
    public static LuckRecoveryResult RollLuckRecovery(CharacterSheet sheet, IDiceRoller dice)
    {
        var oldValue = sheet.Current.Luck;
        var roll = dice.Percentile();

        if (roll <= oldValue)
            return new LuckRecoveryResult(roll, oldValue, false, 0, oldValue);

        var newValue = Math.Min(DerivedAttributeRules.MaxLuck, oldValue + dice.Roll(LuckGain.Count, LuckGain.Sides));
        sheet.Current.Luck = newValue;
        return new LuckRecoveryResult(roll, oldValue, true, newValue - oldValue, newValue);
    }

    /// <summary>
    /// Самолечение (стр. 165): проверка Рассудка. Успех — +1d6, провал — −1 и пункт биографии меняется.
    /// Ключевая связь даёт бонусную кость: при успехе лечит бессрочное безумие, при провале теряется.
    /// Порог — общий <see cref="Check"/> (в v1 здесь было <c>roll &lt;= Рассудок</c> в обход него).
    /// </summary>
    public static SelfHealingResult RollSelfHealing(
        CharacterSheet sheet, SkillCatalog catalog, bool useKeyConnection, IDiceRoller dice)
    {
        var roll = dice.Percentile(bonusDice: useKeyConnection ? 1 : 0);
        var success = Check.Evaluate(roll, sheet.Current.Sanity).IsSuccess();

        if (!success)
        {
            var lost = Math.Min(1, Math.Max(0, sheet.Current.Sanity));
            sheet.Current.Sanity -= lost;

            if (useKeyConnection)
                sheet.Biography.KeyConnection = string.Empty;

            return new SelfHealingResult(roll, useKeyConnection, false, false, -lost, false, useKeyConnection);
        }

        var gain = SanityRules.Grant(sheet, catalog, dice.Roll(SelfHealingGain.Count, SelfHealingGain.Sides));

        var curedIndefinite = useKeyConnection && sheet.Condition.IndefiniteInsanity;
        if (curedIndefinite)
        {
            sheet.Condition.IndefiniteInsanity = false;
            sheet.Condition.IndefiniteInsanityStartedAt = null;
        }

        // Критический успех позволяет сразу назначить новую ключевую связь взамен утраченной.
        return new SelfHealingResult(roll, useKeyConnection, true, roll == 1, gain, curedIndefinite, false);
    }

    /// <summary>Варианты «занятия и Средства» (стр. 94) от лучшего к худшему.</summary>
    public static IReadOnlyList<CreditRatingOption> CreditRatingOptions { get; } =
    [
        new(CreditRatingChange.Rich, "Я богат!", "+1d10",
            "Активы выросли настолько, что не соответствуют достатку: бросайте, пока Средства не дойдут до нужного уровня."),
        new(CreditRatingChange.Promotion, "Дела идут на лад", "+1d6",
            "Сыщик получил повышение."),
        new(CreditRatingChange.BusinessAsUsual, "Жизнь идёт своим чередом", "—",
            "Ничего, что заметно повлияло бы на доходы, не произошло."),
        new(CreditRatingChange.TightenBelt, "Пора затянуть пояс", "−1d10",
            "Сыщика понизили или он взял отпуск за свой счёт."),
        new(CreditRatingChange.SoldSilver, "Продал фамильное серебро", "−1d10",
            "Состояние сыщика упало до обычных активов более низкого достатка."),
        new(CreditRatingChange.RoughPatch, "Чёрная полоса", "−2d10",
            "Основной источник доходов потерян; при пособии по безработице Средства не опустятся ниже 1d10 − 1."),
        new(CreditRatingChange.Bankrupt, "Банкрот!", "−1d100",
            "Доходов нет и/или срочно нужно платить по долгам."),
    ];

    /// <summary>Меняет Средства по варианту; значение не выходит за 0–99.</summary>
    public static CreditRatingResult ApplyCreditRatingChange(
        CharacterSheet sheet, SkillCatalog catalog, CreditRatingChange change, IDiceRoller dice)
    {
        var oldValue = sheet.Value(catalog, SkillCodes.CreditRating);

        var sign = change is CreditRatingChange.Rich or CreditRatingChange.Promotion ? 1 : -1;
        var (roll, delta) = CreditRatingDice(change) is { } spec ? RollDelta(spec.Count, spec.Sides, sign) : (0, 0);

        var newValue = Math.Clamp(oldValue + delta, 0, 99);
        if (sheet.EnsureEntry(catalog, SkillCodes.CreditRating) is { } skill)
            skill.Value = newValue;

        return new CreditRatingResult(change, roll, oldValue, newValue);

        (int Roll, int Delta) RollDelta(int count, int sides, int sign)
        {
            var value = dice.Roll(count, sides);
            return (value, sign * value);
        }
    }

    /// <summary>
    /// Деньги в фазу развития (стр. 94): к оставшимся наличным — столбец «Наличные» строки таблицы II по нынешним Средствам,
    /// раз за фазу. Карманные и активы приводятся к достатку, только если он сменился (<paramref name="creditRatingBefore"/> —
    /// Средства до шага) или они не заполнены: по умолчанию все остаются при своём, и «дом в Аркхеме» числом таблицы не затирается.
    /// </summary>
    public static FinancesUpdateResult RecalculateFinances(CharacterSheet sheet, SkillCatalog catalog, Era era, int creditRatingBefore)
    {
        var creditRating = sheet.Value(catalog, SkillCodes.CreditRating);
        var tier = FinanceRules.GetTier(creditRating, era);
        var tierChanged = FinanceRules.GetTier(creditRatingBefore, era).Name != tier.Name;

        var remaining = sheet.Finances.Cash;
        var cash = (remaining ?? 0) + tier.Cash;

        sheet.Finances.Cash = cash;
        if (tierChanged || sheet.Finances.PocketMoney is null)
            sheet.Finances.PocketMoney = tier.PocketMoney;
        if (tierChanged || string.IsNullOrWhiteSpace(sheet.Finances.Assets))
            sheet.Finances.Assets = tier.AssetsText;

        return new FinancesUpdateResult(tier.Name, creditRating, remaining, tier.Cash, cash, sheet.Finances.Assets,
            sheet.Finances.PocketMoney ?? tier.PocketMoney, tierChanged);
    }

    /// <summary>«Время лечит» (стр. 167): накопленное привыкание к каждому виду снижается на 1.</summary>
    public static int RelaxHabituations(CharacterSheet sheet)
    {
        var affected = 0;
        foreach (var habituation in sheet.Condition.Habituations.Where(h => h.LostSanity > 0))
        {
            habituation.LostSanity--;
            affected++;
        }

        return affected;
    }
}

/// <summary>Итог одной проверки опыта.</summary>
public sealed record SkillImprovementResult
{
    public required string SkillName { get; init; }
    public required int OldValue { get; init; }
    public required int Roll { get; init; }
    public bool Improved { get; init; }
    public int Gain { get; init; }
    public int NewValue { get; init; }

    /// <summary>Навык впервые дошёл до 90%.</summary>
    public bool ReachedMastery { get; init; }

    public int SanityGain { get; init; }
}

public sealed record LuckRecoveryResult(int Roll, int OldValue, bool Improved, int Gain, int NewValue);

public sealed record SelfHealingResult(
    int Roll,
    bool UsedKeyConnection,
    bool Success,
    bool CriticalSuccess,
    int SanityDelta,
    bool CuredIndefiniteInsanity,
    bool LostKeyConnection);

public enum CreditRatingChange
{
    Rich,
    Promotion,
    BusinessAsUsual,
    TightenBelt,
    SoldSilver,
    RoughPatch,
    Bankrupt,
}

public sealed record CreditRatingOption(CreditRatingChange Change, string Title, string DiceLabel, string Description);

public sealed record CreditRatingResult(CreditRatingChange Change, int Roll, int OldValue, int NewValue);

public sealed record FinancesUpdateResult(
    string TierName,
    int CreditRating,
    decimal? PreviousCash,
    decimal TierCash,
    decimal NewCash,
    string Assets,
    decimal PocketMoney,
    bool TierChanged);

/// <summary>Кости правила: <c>Count</c>d<c>Sides</c> («2d6») — окно по ним предлагает вписать сумму.</summary>
public sealed record DiceSpec(int Count, int Sides)
{
    public override string ToString() => $"{Count}d{Sides}";
}
