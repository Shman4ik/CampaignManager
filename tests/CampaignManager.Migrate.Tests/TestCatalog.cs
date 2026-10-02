using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Migrate.Catalogs;

namespace CampaignManager.Migrate.Tests;

/// <summary>
/// Справочник навыков из таблицы кодов Core — тот же состав, что перенос кладёт в <c>cm.skills</c>. Id другие,
/// чем в v1, поэтому листы находят навыки по имени, а не по <c>SkillModelId</c>: так проверяется сопоставление.
/// </summary>
public static class TestCatalog
{
    public static SkillCatalog Skills { get; } = Build();

    public static SkillResolver Resolver { get; } = new(Skills);

    public static Guid Id(string code) => Skills.FindByCode(code)!.Id;

    public static JsonObject Sheet(string fixture) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Sheets", fixture + ".json")))!.AsObject();

    private static SkillCatalog Build()
    {
        var bases = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["skill.fighting.brawl"] = 25,
            ["skill.firearms.handgun"] = 20,
            ["skill.language-other"] = 1,
        };
        return new SkillCatalog(SkillCodes.BookNames.Select(pair => new SkillDefinition(StableId(pair.Key), pair.Value)
        {
            Code = pair.Key,
            ParentId = SkillCodes.ParentOf(pair.Key) is { } parent ? StableId(parent) : null,
            BaseValue = bases.GetValueOrDefault(pair.Key),
            BaseFormula = pair.Key switch
            {
                SkillCodes.Dodge => "DEX/2",
                SkillCodes.LanguageOwn => "EDU",
                _ => null,
            },
        }));
    }

    private static Guid StableId(string code) => new(SHA256.HashData(Encoding.UTF8.GetBytes(code)).AsSpan(0, 16));
}
