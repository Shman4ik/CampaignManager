using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Web.Components.Features.Characters.Services;

/// <summary>
///     Фаза развития сыщиков («Зов Ктулху» 7e): броски опыта и Средства — стр. 92–94,
///     восстановление Удачи — стр. 93, награда Хранителя, самолечение и привыкание
///     к ужасному — стр. 164–167.
///     Единственное место, где живут эти формулы: лист персонажа только показывает результат.
/// </summary>
public static class DevelopmentPhaseRules
{
    /// <summary>Навык, доросший до 90%, даёт сыщику +2d6 к текущему Рассудку (стр. 92).</summary>
    public const int MasteryThreshold = 90;

    /// <summary>Результат выше 95 улучшает навык, даже если он и так высок (стр. 92).</summary>
    public const int AlwaysImprovesAbove = 95;

    /// <summary>Удача меняется по ходу игры, но никогда не превышает 99 (стр. 93).</summary>
    public const int MaxLuck = 99;

    private const string MythosSkillName = "Мифы Ктулху";
    private const string CreditRatingSkillName = "Средства";
    private const string DodgeSkillName = "Уклонение";

    /// <summary>
    ///     Мифы Ктулху и Средства никогда не отмечают — «у этих навыков даже нет места
    ///     для галочки» (стр. 92).
    /// </summary>
    public static bool CanBeChecked(string skillName) =>
        skillName is not (MythosSkillName or CreditRatingSkillName);

    /// <summary>Отмеченные навыки, по которым в эту фазу бросают проверку опыта.</summary>
    public static List<Skill> GetCheckedSkills(Character character) =>
        character.Skills.SkillGroups
            .SelectMany(g => g.Skills)
            .Where(s => s.IsUsed && CanBeChecked(s.Name))
            .OrderBy(s => s.Name, StringComparer.CurrentCulture)
            .ToList();

    #region Проверки опыта

    /// <summary>
    ///     Проверка опыта по одному навыку: 1d100 больше текущего значения (или выше 95) —
    ///     навык растёт на 1d10, иначе не меняется (стр. 92). Значение может превысить 100%.
    /// </summary>
    public static SkillImprovementResult RollSkillImprovement(Character character, Skill skill)
    {
        var oldValue = skill.Value.Regular;
        var roll = Dice.Percentile();
        var improved = roll > oldValue || roll > AlwaysImprovesAbove;

        if (!improved)
            return new SkillImprovementResult
            {
                SkillName = skill.Name,
                OldValue = oldValue,
                Roll = roll,
                NewValue = oldValue
            };

        var gain = Dice.Roll(1, 10);
        skill.Value.Regular = oldValue + gain;
        skill.Value.UpdateDerived();

        // Уклонение живёт на листе дважды: навык — хозяин, PersonalInfo.Dodge читает боёвка.
        if (skill.Name is DodgeSkillName)
            DerivedAttributeRules.SyncDodgeFromSkill(character);

        var reachedMastery = oldValue < MasteryThreshold && skill.Value.Regular >= MasteryThreshold;
        var sanityGain = reachedMastery ? GrantSanity(character, Dice.Roll(2, 6)) : 0;

        return new SkillImprovementResult
        {
            SkillName = skill.Name,
            OldValue = oldValue,
            Roll = roll,
            Improved = true,
            Gain = gain,
            NewValue = skill.Value.Regular,
            ReachedMastery = reachedMastery,
            SanityGain = sanityGain
        };
    }

    /// <summary>
    ///     Стирает отметки со всех навыков — последний шаг фазы (стр. 92).
    ///     В следующий раз навык отметят заново.
    /// </summary>
    public static void ClearSkillChecks(Character character)
    {
        foreach (var skill in character.Skills.SkillGroups.SelectMany(g => g.Skills))
            skill.IsUsed = false;
    }

    #endregion

    #region Удача

    /// <summary>
    ///     Восстановление Удачи (стр. 93): 1d100 больше текущей Удачи — прибавьте 1d10,
    ///     но не выше 99. Часть необязательного правила о пунктах Удачи.
    /// </summary>
    public static LuckRecoveryResult RollLuckRecovery(Character character)
    {
        var luck = character.DerivedAttributes.Luck;
        var oldValue = luck.Value;
        var roll = Dice.Percentile();

        if (roll <= oldValue)
            return new LuckRecoveryResult(roll, oldValue, false, 0, oldValue);

        var gain = Dice.Roll(1, 10);
        var newValue = Math.Min(MaxLuck, oldValue + gain);
        luck.Value = newValue;
        luck.MaxValue = MaxLuck;

        return new LuckRecoveryResult(roll, oldValue, true, newValue - oldValue, newValue);
    }

    #endregion

    #region Рассудок: награда Хранителя и самолечение

    /// <summary>
    ///     Прибавляет пункты рассудка, не давая выйти за максимум (99 − Мифы Ктулху).
    ///     Возвращает, сколько прибавилось на самом деле.
    /// </summary>
    public static int GrantSanity(Character character, int amount)
    {
        var max = SanityRules.ComputeMaxSanity(character);
        var sanity = character.DerivedAttributes.Sanity;
        sanity.MaxValue = max;

        var newValue = Math.Min(max, sanity.Value + amount);
        var actual = newValue - sanity.Value;
        sanity.Value = newValue;
        return actual;
    }

    /// <summary>
    ///     Самолечение (стр. 165): сыщик тратит время на пункт своей биографии и проходит
    ///     проверку Рассудка. Успех — +1d6 рассудка, провал — −1 и пункт биографии меняется.
    ///     Ключевая связь даёт бонусную кость: при успехе она же лечит бессрочное безумие,
    ///     при провале связь теряется.
    /// </summary>
    public static SelfHealingResult RollSelfHealing(Character character, bool useKeyConnection)
    {
        var sanity = character.DerivedAttributes.Sanity;
        var roll = useKeyConnection ? Dice.PercentileWithBonusDie() : Dice.Percentile();
        var success = roll <= sanity.Value;

        if (!success)
        {
            var lost = Math.Min(1, sanity.Value);
            sanity.Value -= lost;

            var lostKeyConnection = useKeyConnection;
            if (lostKeyConnection)
                character.Biography.KeyConnection = string.Empty;

            return new SelfHealingResult(roll, useKeyConnection, false, false, -lost, false, lostKeyConnection);
        }

        var gain = GrantSanity(character, Dice.Roll(1, 6));

        var curedIndefinite = useKeyConnection && character.State.HasIndefiniteInsanity;
        if (curedIndefinite)
        {
            character.State.HasIndefiniteInsanity = false;
            character.State.IndefiniteInsanityStartedAt = null;
        }

        // Критический успех сразу позволяет назначить новую ключевую связь взамен утраченной.
        return new SelfHealingResult(roll, useKeyConnection, true, roll == 1, gain, curedIndefinite, false);
    }

    #endregion

    #region Средства и занятия

    /// <summary>
    ///     Варианты из «Фаза развития сыщиков: занятия и Средства» (стр. 94),
    ///     перечисленные от лучшего к худшему.
    /// </summary>
    public static CreditRatingOption[] CreditRatingOptions =>
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
            "Доходов нет и/или срочно нужно платить по долгам.")
    ];

    /// <summary>Меняет навык «Средства» по выбранному варианту. Значение не выходит за 0–99.</summary>
    public static CreditRatingResult ApplyCreditRatingChange(Character character, CreditRatingChange change)
    {
        var skill = FindCreditRatingSkill(character);
        var oldValue = skill?.Value.Regular ?? 0;

        var (roll, delta) = change switch
        {
            CreditRatingChange.Rich => RollDelta(1, 10, 1),
            CreditRatingChange.Promotion => RollDelta(1, 6, 1),
            CreditRatingChange.BusinessAsUsual => (0, 0),
            CreditRatingChange.TightenBelt => RollDelta(1, 10, -1),
            CreditRatingChange.SoldSilver => RollDelta(1, 10, -1),
            CreditRatingChange.RoughPatch => RollDelta(2, 10, -1),
            CreditRatingChange.Bankrupt => RollDelta(1, 100, -1),
            _ => (0, 0)
        };

        var newValue = Math.Clamp(oldValue + delta, 0, 99);

        if (skill is not null)
        {
            skill.Value.Regular = newValue;
            skill.Value.UpdateDerived();
        }

        return new CreditRatingResult(change, roll, oldValue, newValue);

        static (int Roll, int Delta) RollDelta(int count, int sides, int sign)
        {
            var roll = Dice.Roll(count, sides);
            return (roll, sign * roll);
        }
    }

    /// <summary>
    ///     Пересчёт денег после смены Средств (стр. 94): к оставшимся наличным прибавляют
    ///     столбец «Наличные» новой строки таблицы II, активы и карманные деньги
    ///     приводят к новому достатку.
    /// </summary>
    public static FinancesUpdateResult RecalculateFinances(Character character, bool isModern)
    {
        var creditRating = FindCreditRatingSkill(character)?.Value.Regular ?? 0;
        var tier = FinanceRules.GetTier(creditRating, isModern);

        var remaining = FinanceRules.TryParseMoney(character.Finances.Cash);
        var cash = remaining is null ? tier.Cash : remaining.Value + tier.Cash;

        character.Finances.Cash = $"${FinanceRules.Format(cash)}";
        character.Finances.PocketMoney = $"${tier.PocketMoneyText}";
        character.Finances.Assets = [tier.Assets is null ? tier.AssetsText : $"${tier.AssetsText}"];

        return new FinancesUpdateResult(tier.Name, creditRating, remaining, tier.Cash, cash,
            character.Finances.Assets[0], character.Finances.PocketMoney);
    }

    private static Skill? FindCreditRatingSkill(Character character) =>
        character.Skills.SkillGroups
            .SelectMany(g => g.Skills)
            .FirstOrDefault(s => string.Equals(s.Name, CreditRatingSkillName, StringComparison.Ordinal));

    #endregion

    #region Привыкание к ужасному

    /// <summary>
    ///     «Время лечит» (стр. 167): в каждую фазу развития накопленная потеря рассудка
    ///     за каждый вид тварей снижается на 1. Возвращает число затронутых записей.
    /// </summary>
    public static int RelaxHabituations(Character character)
    {
        var affected = 0;

        foreach (var habituation in character.State.MythosHabituations)
        {
            if (habituation.LostSanity <= 0)
                continue;

            habituation.LostSanity--;
            affected++;
        }

        return affected;
    }

    #endregion
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

    /// <summary>Навык впервые дошёл до 90% — за это положены 2d6 рассудка.</summary>
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
    Bankrupt
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
    string PocketMoney);
