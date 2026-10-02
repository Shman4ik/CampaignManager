using System.Text.Json;
using System.Text.Json.Serialization;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Encounters;

namespace CampaignManager.Core.Documents;

/// <summary>
/// Сериализация документов (лист, статблок, состояние сцены) и черновика помощника — одна на сервер,
/// WebAssembly и мобильное приложение. Только source-generated контекст: Core обрезается и собирается
/// AOT (D4). JSON в camelCase, enum'ы строками (SCHEMA, правило 6) — в v1 журнал боя и стадии книг
/// лежали числами, и порядок членов нельзя было трогать.
/// </summary>
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CharacterSheet))]
[JsonSerializable(typeof(Statblock))]
[JsonSerializable(typeof(EncounterState))]
[JsonSerializable(typeof(InvestigatorDraft))]
public sealed partial class CmJsonContext : JsonSerializerContext;

/// <summary>Чтение и запись документов с приведением старых версий 2.0 к текущей.</summary>
public static class CmJson
{
    public static JsonSerializerOptions Options => CmJsonContext.Default.Options;

    /// <summary>
    /// Лист из колонки <c>sheet</c> версии <paramref name="version"/>. Старая версия 2.0 сначала
    /// приводится к текущей (<see cref="DocumentUpgrader"/>); неизвестные поля сохраняются.
    /// </summary>
    public static CharacterSheet ReadSheet(JsonDocument json, int version) =>
        Read(DocumentKind.CharacterSheet, json, version, CmJsonContext.Default.CharacterSheet);

    public static Statblock ReadStatblock(JsonDocument json, int version) =>
        Read(DocumentKind.Statblock, json, version, CmJsonContext.Default.Statblock);

    public static EncounterState ReadEncounterState(JsonDocument json, int version) =>
        Read(DocumentKind.EncounterState, json, version, CmJsonContext.Default.EncounterState);

    /// <summary>Документ для колонки; версия — <c>CurrentVersion</c> его типа.</summary>
    public static JsonDocument Write(CharacterSheet sheet) =>
        JsonSerializer.SerializeToDocument(sheet, CmJsonContext.Default.CharacterSheet);

    public static JsonDocument Write(Statblock statblock) =>
        JsonSerializer.SerializeToDocument(statblock, CmJsonContext.Default.Statblock);

    public static JsonDocument Write(EncounterState state) =>
        JsonSerializer.SerializeToDocument(state, CmJsonContext.Default.EncounterState);

    /// <summary>Лист строкой — слепок для автосохранения и черновика (сравнение «изменилось ли»).</summary>
    public static string Serialize(CharacterSheet sheet) =>
        JsonSerializer.Serialize(sheet, CmJsonContext.Default.CharacterSheet);

    public static CharacterSheet? DeserializeSheet(string json) =>
        JsonSerializer.Deserialize(json, CmJsonContext.Default.CharacterSheet);

    /// <summary>
    /// Глубокая копия листа — для предпросмотра правила (окно чтения книги применяет его к копии и
    /// показывает итог, а не считает Мифы и рассудок второй раз сам, как в v1).
    /// </summary>
    public static CharacterSheet Clone(CharacterSheet sheet) =>
        DeserializeSheet(Serialize(sheet)) ?? throw new JsonException("Копия листа пуста.");

    public static string Serialize(InvestigatorDraft draft) =>
        JsonSerializer.Serialize(draft, CmJsonContext.Default.InvestigatorDraft);

    public static InvestigatorDraft? DeserializeDraft(string json) =>
        JsonSerializer.Deserialize(json, CmJsonContext.Default.InvestigatorDraft);

    private static T Read<T>(DocumentKind kind, JsonDocument json, int version,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        if (version >= DocumentUpgrader.CurrentVersion(kind))
            return json.Deserialize(typeInfo) ?? throw EmptyDocument(kind);

        var upgraded = DocumentUpgrader.Upgrade(kind, json.RootElement, version);
        return upgraded.Deserialize(typeInfo) ?? throw EmptyDocument(kind);
    }

    private static JsonException EmptyDocument(DocumentKind kind) => new($"Документ {kind} пуст (null).");
}
