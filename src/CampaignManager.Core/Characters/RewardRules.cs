namespace CampaignManager.Core.Characters;

/// <summary>Что получает (или теряет) сыщик по итогам сценария.</summary>
public enum RewardKind
{
    /// <summary>Прибавка Рассудка по формуле (обычно 1d6): достаётся всем, но каждый бросает сам (стр. 165).</summary>
    SanityGain,

    /// <summary>Проверка Рассудка с потерей «успех/провал» — «1/1d6, если пленников не спасли» (стр. 359, 379).</summary>
    SanityCheck,

    /// <summary>Денежное вознаграждение — к наличным (стр. 94).</summary>
    Money,
}

/// <summary>
/// Итоги сценария для партии (гл. 8, 15): одна награда — одна запись в лист каждого сыщика. Броски решает окно (каждый бросает
/// сам или вписывает со стола), в лист идёт уже решённое число: выпавшая прибавка, потеря по уровню проверки или сумма.
/// </summary>
public static class RewardRules
{
    /// <summary>
    /// Записывает награду в лист и возвращает строку «было → стало». Прибавка — не выше максимума (<see cref="SanityRules.Grant"/>);
    /// потеря — одна причина (<see cref="SanityRules.ApplyLoss"/>: 5+ — проверка ИНТ, за день — порог бессрочного, это покажет лист);
    /// деньги — к наличным (<see cref="FinanceRules.Receive"/>).
    /// </summary>
    public static string Apply(CharacterSheet sheet, SkillCatalog catalog, RewardKind kind, decimal amount)
    {
        if (kind == RewardKind.Money)
        {
            var cash = sheet.Finances.Cash ?? 0;
            FinanceRules.Receive(sheet.Finances, amount);
            return $"наличные {Terms.Dollars(cash)} → {Terms.Dollars(sheet.Finances.Cash ?? 0)}";
        }

        var before = sheet.Current.Sanity;
        var points = (int)Math.Max(0, amount);
        if (kind == RewardKind.SanityGain)
            SanityRules.Grant(sheet, catalog, points);
        else if (points > 0)
            SanityRules.ApplyLoss(sheet, catalog, points);
        return $"Рассудок {before} → {sheet.Current.Sanity}";
    }
}
