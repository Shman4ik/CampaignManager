using System.Globalization;
using System.Text.Json.Nodes;
using Npgsql;

namespace CampaignManager.Migrate.V1;

/// <summary>
/// Снимок v1: каждая таблица <c>games</c>/<c>identity</c> строками <see cref="JsonObject"/> (<c>row_to_json</c>).
/// Типов v1 у переноса нет намеренно — схема v1 уходит в T3.3, а перенос читает только нужные колонки по имени.
/// Только чтение: в схемы v1 перенос не пишет.
/// </summary>
public sealed class V1Database
{
    public required IReadOnlyList<JsonObject> Users { get; init; }
    public required IReadOnlyList<JsonObject> UserPreferences { get; init; }
    public required IReadOnlyList<JsonObject> KeeperApplications { get; init; }
    public required IReadOnlyList<JsonObject> Campaigns { get; init; }
    public required IReadOnlyList<JsonObject> CampaignPlayers { get; init; }
    public required IReadOnlyList<JsonObject> CampaignSessions { get; init; }
    public required IReadOnlyList<JsonObject> Scenarios { get; init; }
    public required IReadOnlyList<JsonObject> ScenarioNpcs { get; init; }
    public required IReadOnlyList<JsonObject> Characters { get; init; }
    public required IReadOnlyList<JsonObject> Skills { get; init; }
    public required IReadOnlyList<JsonObject> Occupations { get; init; }
    public required IReadOnlyList<JsonObject> Weapons { get; init; }
    public required IReadOnlyList<JsonObject> Spells { get; init; }
    public required IReadOnlyList<JsonObject> Books { get; init; }
    public required IReadOnlyList<JsonObject> Items { get; init; }
    public required IReadOnlyList<JsonObject> Creatures { get; init; }
    public required IReadOnlyList<JsonObject> MusicTracks { get; init; }
    public required IReadOnlyList<JsonObject> EditHistory { get; init; }
    public required IReadOnlyList<JsonObject> ChaseSessions { get; init; }
    public required IReadOnlyList<JsonObject> DataProtectionKeys { get; init; }

    public static async Task<V1Database> ReadAsync(NpgsqlDataSource source, CancellationToken cancellationToken)
    {
        async Task<IReadOnlyList<JsonObject>> Table(string schema, string table, string order = "\"Id\"")
        {
            await using var command = source.CreateCommand(
                $"SELECT row_to_json(t)::text FROM {schema}.\"{table}\" t ORDER BY {order}");
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            List<JsonObject> rows = [];
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(JsonNode.Parse(reader.GetString(0))!.AsObject());
            }

            return rows;
        }

        return new V1Database
        {
            Users = await Table("identity", "AspNetUsers"),
            UserPreferences = await Table("games", "UserPreferences"),
            KeeperApplications = await Table("games", "KeeperApplications"),
            Campaigns = await Table("games", "Campaigns"),
            CampaignPlayers = await Table("games", "CampaignPlayers"),
            CampaignSessions = await Table("games", "CampaignSessions"),
            Scenarios = await Table("games", "Scenarios"),
            ScenarioNpcs = await Table("games", "ScenarioNpcs"),
            Characters = await Table("games", "Characters"),
            Skills = await Table("games", "Skills"),
            Occupations = await Table("games", "Occupations"),
            Weapons = await Table("games", "Weapons"),
            Spells = await Table("games", "Spells"),
            Books = await Table("games", "Books"),
            Items = await Table("games", "Items"),
            Creatures = await Table("games", "Creatures"),
            MusicTracks = await Table("games", "MusicTracks"),
            EditHistory = await Table("games", "EditHistoryEntries", "\"CreatedAt\", \"Id\""),
            ChaseSessions = await Table("games", "ChaseSessions"),
            DataProtectionKeys = await Table("games", "DataProtectionKeys"),
        };
    }
}

/// <summary>Чтение полей строки v1 по имени колонки или ключу JSON.</summary>
public static class V1Json
{
    public static string? Str(this JsonNode? node, string key) =>
        node?[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : node?[key]?.ToString();

    /// <summary>Строка без пробелов по краям; пустая — null.</summary>
    public static string? Text(this JsonNode? node, string key) =>
        node.Str(key)?.Trim() is { Length: > 0 } text ? text : null;

    public static Guid? Guid(this JsonNode? node, string key) =>
        System.Guid.TryParse(node.Str(key), out var id) ? id : null;

    public static int? Int(this JsonNode? node, string key) => node?[key] switch
    {
        JsonValue value when value.TryGetValue<int>(out var number) => number,
        JsonValue value when value.TryGetValue<double>(out var real) => (int)real,
        JsonValue value when value.TryGetValue<string>(out var text)
            && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => null,
    };

    public static bool Bool(this JsonNode? node, string key) =>
        node?[key] is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

    /// <summary>Время v1. <c>timestamptz</c> приходит со смещением; без него (legacy) считается UTC.</summary>
    public static DateTimeOffset? Time(this JsonNode? node, string key) =>
        node.Str(key) is { Length: > 0 } text
        && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time)
            ? time.ToUniversalTime()
            : null;

    public static JsonArray Arr(this JsonNode? node, string key) => node?[key] as JsonArray ?? [];

    public static JsonObject? Obj(this JsonNode? node, string key) => node?[key] as JsonObject;

    /// <summary>Непустые строки jsonb-списка без пробелов по краям.</summary>
    public static List<string> Strings(this JsonNode? node, string key) =>
        node.Arr(key)
            .Select(item => item?.ToString().Trim())
            .OfType<string>()
            .Where(text => text.Length > 0)
            .ToList();

    /// <summary>v1 пишет enum'ы где именем, где числом — принимаем оба.</summary>
    public static TEnum? Enum<TEnum>(this JsonNode? node, string key)
        where TEnum : struct, System.Enum
    {
        var raw = node.Str(key);
        if (raw is null)
        {
            return null;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return System.Enum.IsDefined(typeof(TEnum), number) ? (TEnum)(object)number : null;
        }

        return System.Enum.TryParse<TEnum>(raw, ignoreCase: true, out var parsed) ? parsed : null;
    }
}
