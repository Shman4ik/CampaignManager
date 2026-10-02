namespace CampaignManager.Core.Checks;

/// <summary>Строка групповой проверки: сыщик, его значение навыка и итог броска (null — ещё не бросали).</summary>
public sealed record GroupCheckRow(Guid Id, string Name, int Value, CheckOutcome? Outcome);

/// <summary>
/// Групповая проверка восприятия (гл. 10, стр. 200–201): один навык за всех сыщиков, зацепку получает
/// лучший. В v1 правило выбора лучшего жило в разметке <c>GroupCheckPanel</c>.
/// </summary>
public static class GroupCheck
{
    /// <summary>Три навыка восприятия (стр. 199): Внимание, Слух, Психология.</summary>
    public static IReadOnlyList<string> PerceptionSkillCodes { get; } =
        ["skill.spot-hidden", "skill.listen", "skill.psychology"];

    /// <summary>
    /// Лучшие: прошли проверку с наивысшим уровнем успеха, а среди них — с наибольшим значением навыка
    /// (стр. 88, 200). Полное равенство — все такие строки. Никто не прошёл — пусто.
    /// </summary>
    public static IReadOnlySet<Guid> Best(IEnumerable<GroupCheckRow> rows)
    {
        var passed = rows.Where(r => r.Outcome is { Passed: true }).ToList();
        if (passed.Count == 0)
            return new HashSet<Guid>();

        var topLevel = passed.Max(r => r.Outcome!.Level);
        var topValue = passed.Where(r => r.Outcome!.Level == topLevel).Max(r => r.Value);

        return passed
            .Where(r => r.Outcome!.Level == topLevel && r.Value == topValue)
            .Select(r => r.Id)
            .ToHashSet();
    }
}
