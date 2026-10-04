using System.Text.Json;
using System.Text.Json.Nodes;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Encounters;

namespace CampaignManager.Core.Documents;

/// <summary>Вид документа: у каждого своя колонка версии.</summary>
public enum DocumentKind
{
    CharacterSheet,
    Statblock,
    EncounterState,
    InvestigatorDraft,
}

/// <summary>
/// Апкастер документов 2.0: приводит документ старой версии к текущей по шагам «N → N+1» над JSON, до
/// десериализации — шаг видит и поля, которых в текущем типе уже нет.
/// <para>
/// <b>Листы v1 он не переводит</b>: это делает перенос (T1.3) один раз — ему нужны справочник навыков и
/// сопоставление по имени, которых в Core нет (SCHEMA, «Документы»). Первая версия 2.0 — 1, шагов пока
/// нет. Новая версия документа: поднять <c>CurrentVersion</c> типа и добавить шаг в <see cref="Steps"/>;
/// тест проверяет, что у каждой версии ниже текущей шаг есть.
/// </para>
/// </summary>
public static class DocumentUpgrader
{
    /// <summary>Самая ранняя версия, которую апкастер принимает.</summary>
    public const int FirstVersion = 1;

    /// <summary>Шаги: (вид, версия «откуда») → преобразование в следующую версию.</summary>
    internal static IReadOnlyDictionary<(DocumentKind Kind, int From), Func<JsonObject, JsonObject>> Steps { get; } =
        new Dictionary<(DocumentKind, int), Func<JsonObject, JsonObject>>();

    public static int CurrentVersion(DocumentKind kind) => kind switch
    {
        DocumentKind.CharacterSheet => CharacterSheet.CurrentVersion,
        DocumentKind.Statblock => Statblock.CurrentVersion,
        DocumentKind.EncounterState => EncounterState.CurrentVersion,
        DocumentKind.InvestigatorDraft => InvestigatorDraft.CurrentVersion,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>
    /// Документ версии <paramref name="fromVersion"/> в текущей версии. Версия новее текущей не трогается:
    /// её читает старое приложение, и неизвестные поля доживут до записи в <see cref="DocumentPart.Extra"/>.
    /// </summary>
    public static JsonObject Upgrade(DocumentKind kind, JsonElement document, int fromVersion)
    {
        if (fromVersion < FirstVersion)
            throw new NotSupportedException(
                $"{kind} версии {fromVersion}: документы v1 переводит перенос (T1.3), а не апкастер.");

        var node = JsonNode.Parse(document.GetRawText()) as JsonObject
                   ?? throw new JsonException($"{kind}: документ — не JSON-объект.");

        for (var version = fromVersion; version < CurrentVersion(kind); version++)
        {
            if (!Steps.TryGetValue((kind, version), out var step))
                throw new NotSupportedException($"{kind}: нет шага с версии {version} на {version + 1}.");
            node = step(node);
        }

        return node;
    }
}
