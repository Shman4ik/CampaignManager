using System.Text.Json;
using System.Text.Json.Serialization;
using CampaignManager.Contracts.Files;
using CampaignManager.Contracts.Identity;
using CampaignManager.Contracts.Platform;

namespace CampaignManager.Contracts;

/// <summary>
/// Единственный способ сериализовать DTO API: сгенерированный контекст вместо рефлексии,
/// чтобы клиент работал и под AOT (мобильное приложение, D4). Новый DTO модуля
/// добавляется сюда атрибутом. Сервер ставит этот контекст первым в цепочку резолверов.
/// Enum'ы — строками с именем члена, как в колонках и документах (SCHEMA, правило 5).
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, UseStringEnumConverter = true)]
[JsonSerializable(typeof(PingResponse))]
[JsonSerializable(typeof(StoredFileDto))]
[JsonSerializable(typeof(AddExternalFileRequest))]
[JsonSerializable(typeof(OrphanFilesReport))]
[JsonSerializable(typeof(DeleteOrphansRequest))]
[JsonSerializable(typeof(DeleteOrphansResponse))]
[JsonSerializable(typeof(MeResponse))]
public sealed partial class ContractsJsonContext : JsonSerializerContext;
