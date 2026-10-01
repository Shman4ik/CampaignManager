using System.Reflection;
using Xunit;

namespace CampaignManager.Server.Tests;

/// <summary>
/// Границы между проектами 2.0 (docs/v2/README.md, «Зависимости между проектами») — по ссылкам
/// скомпилированных сборок. В метаданных остаются только те ссылки, чьи типы реально используются,
/// поэтому лишняя ProjectReference без кода тест не уронит, а первый же вызов через неё — уронит.
/// </summary>
public sealed class ArchitectureTests
{
    private const string Own = "CampaignManager.";

    /// <summary>Сборка → какие наши сборки ей можно видеть, в том числе транзитивно.</summary>
    public static TheoryData<string, string[]> AllowedOwnDependencies => new()
    {
        { "CampaignManager.Core", [] },
        { "CampaignManager.Contracts", ["CampaignManager.Core"] },
        { "CampaignManager.ApiClient", ["CampaignManager.Core", "CampaignManager.Contracts"] },
        // UI ходит только в интерфейсы Contracts и в Core: ни в Data, ни в Server, ни в ApiClient.
        { "CampaignManager.UI", ["CampaignManager.Core", "CampaignManager.Contracts"] },
        { "CampaignManager.Data", ["CampaignManager.Core"] },
    };

    [Theory]
    [MemberData(nameof(AllowedOwnDependencies))]
    public void Project_sees_only_allowed_projects(string assemblyName, string[] allowed)
    {
        var seen = OwnClosure(assemblyName).Where(name => name != assemblyName);

        Assert.All(seen, name => Assert.Contains(name, allowed));
    }

    // Core и Contracts общие с мобильным приложением: только BCL, никаких пакетов.
    [Theory]
    [InlineData("CampaignManager.Core")]
    [InlineData("CampaignManager.Contracts")]
    public void Shared_project_references_only_bcl_and_own_projects(string assemblyName)
    {
        var foreign = References(assemblyName).Where(name => !name.StartsWith(Own, StringComparison.Ordinal));

        Assert.All(foreign, name => Assert.True(IsBcl(name), $"{assemblyName} ссылается на {name}"));
    }

    [Fact]
    public void UI_does_not_reach_database_through_any_own_project()
    {
        var reached = OwnClosure("CampaignManager.UI").SelectMany(References);

        Assert.DoesNotContain(reached, name =>
            name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || name.StartsWith("Npgsql", StringComparison.Ordinal));
    }

    // Общее с мобильным приложением (D4) должно оставаться безопасным для обрезки и AOT.
    [Theory]
    [InlineData("CampaignManager.Contracts")]
    [InlineData("CampaignManager.ApiClient")]
    public void Shared_project_is_marked_trimmable(string assemblyName)
    {
        var assembly = Assembly.Load(assemblyName);

        Assert.Contains(assembly.GetCustomAttributes<AssemblyMetadataAttribute>(),
            attribute => attribute is { Key: "IsTrimmable", Value: "True" });
    }

    private static IEnumerable<string> References(string assemblyName) =>
        Assembly.Load(assemblyName).GetReferencedAssemblies().Select(reference => reference.Name!);

    private static HashSet<string> OwnClosure(string assemblyName)
    {
        var seen = new HashSet<string>();
        var queue = new Queue<string>([assemblyName]);
        while (queue.TryDequeue(out var name))
        {
            if (!seen.Add(name))
            {
                continue;
            }

            foreach (var reference in References(name).Where(r => r.StartsWith(Own, StringComparison.Ordinal)))
            {
                queue.Enqueue(reference);
            }
        }

        return seen;
    }

    private static bool IsBcl(string name) =>
        name is "netstandard" or "mscorlib" or "System" || name.StartsWith("System.", StringComparison.Ordinal);
}
