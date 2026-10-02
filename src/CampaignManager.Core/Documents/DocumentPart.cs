using System.Text.Json;
using System.Text.Json.Serialization;

namespace CampaignManager.Core.Documents;

/// <summary>
/// Основа каждого объекта внутри документа (лист, статблок, состояние сцены). Поля, которых этот тип
/// не знает, при чтении складываются в <see cref="Extra"/> и при записи возвращаются на место: версия
/// приложения из App Store обновляется с опозданием и не должна стирать то, что добавила новая
/// (SCHEMA, правило 6). Поэтому наследуется каждый вложенный объект, а не только корень — новое поле
/// может появиться и у навыка, и у оружия.
/// </summary>
public abstract record DocumentPart
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
