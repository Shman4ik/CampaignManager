using CampaignManager.Contracts.Scenarios;

namespace CampaignManager.UI.Scenarios;

/// <summary>
/// Дерево локаций для показа. Сервер отдаёт локации уже в порядке дерева (в глубину, соседи по <c>ord</c>), здесь —
/// только глубина и соседи для «Выше»/«Ниже». Режим игры (T2.5b) рисует ту же глубину в боковой панели.
/// </summary>
public static class LocationTree
{
    public static IEnumerable<(ScenarioLocationDto Location, int Depth)> WithDepth(IReadOnlyList<ScenarioLocationDto> locations)
    {
        var depth = new Dictionary<Guid, int>();
        foreach (var location in locations)
        {
            var level = location.ParentId is { } parent && depth.TryGetValue(parent, out var parentDepth) ? parentDepth + 1 : 0;
            depth[location.Id] = level;
            yield return (location, level);
        }
    }

    /// <summary>Соседи локации (тот же родитель) по порядку — их переставляет «Выше»/«Ниже».</summary>
    public static IReadOnlyList<Guid> Siblings(IReadOnlyList<ScenarioLocationDto> locations, ScenarioLocationDto location) =>
        [.. locations.Where(l => l.ParentId == location.ParentId).Select(l => l.Id)];

    /// <summary>Путь «Дом › Подвал» — подпись локации в списке проверок.</summary>
    public static string Path(IReadOnlyList<ScenarioLocationDto> locations, Guid locationId)
    {
        var byId = locations.ToDictionary(l => l.Id);
        var names = new List<string>();
        for (Guid? id = locationId; id is { } current && byId.TryGetValue(current, out var location) && names.Count < 16; id = location.ParentId)
        {
            names.Insert(0, location.Name);
        }

        return string.Join(" › ", names);
    }
}
