using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Web.Components.Features.Characters.Services;

/// <summary>
///     Бонус смежным специализациям («Зов Ктулху» 7e, стр. 76–77).
///     Улучшив одну специализацию до 50% (а затем до 90%), персонаж поднимает все смежные
///     на 10 — но не выше того же порога.
/// </summary>
public static class SpecializationRules
{
    /// <summary>
    ///     Навыки, у которых специализации «имеют много общего» и потому делятся прогрессом.
    ///     Список закрытый: книга прямо противопоставляет им Науку с её астрономией и фармакологией,
    ///     так что вешать бонус на любую группу специализаций нельзя.
    /// </summary>
    private static readonly string[] SharedProgressParents =
    [
        "Ближний бой",
        "Стрельба",
        "Языки",
        "Выживание"
    ];

    public static bool ParentSharesProgress(string? parentSkillName) =>
        parentSkillName is not null &&
        SharedProgressParents.Any(p => parentSkillName.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///     Прибавка, которую навык получает от смежной специализации, или 0.
    ///     Пороги срабатывают по очереди: 50 и 90, каждый даёт +10, но не выше своего порога —
    ///     специализация на 45 при соседе с 50+ поднимается только до 50.
    /// </summary>
    public static int BonusFor(Skill skill, IReadOnlyCollection<Skill> group, string? parentSkillName)
    {
        if (group.Count < 2 || !ParentSharesProgress(parentSkillName))
            return 0;

        var best = group
            .Where(s => !ReferenceEquals(s, skill))
            .Select(s => s.Value.Regular)
            .DefaultIfEmpty(0)
            .Max();

        var cap = best >= 90 ? 90 : best >= 50 ? 50 : 0;
        if (cap == 0 || skill.Value.Regular >= cap)
            return 0;

        return Math.Min(10, cap - skill.Value.Regular);
    }
}
