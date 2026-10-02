using System.Text.Json;
using System.Text.Json.Serialization;
using CampaignManager.Contracts.Files;
using CampaignManager.Contracts.Platform;

namespace CampaignManager.Contracts;

/// <summary>
/// Единственный способ сериализовать DTO API: сгенерированный контекст вместо рефлексии,
/// чтобы клиент работал и под AOT (мобильное приложение, D4). Новый DTO модуля
/// добавляется сюда атрибутом. Сервер ставит этот контекст первым в цепочку резолверов.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(PingResponse))]
[JsonSerializable(typeof(StoredFileDto))]
[JsonSerializable(typeof(AddExternalFileRequest))]
[JsonSerializable(typeof(OrphanFilesReport))]
[JsonSerializable(typeof(DeleteOrphansRequest))]
[JsonSerializable(typeof(DeleteOrphansResponse))]
public sealed partial class ContractsJsonContext : JsonSerializerContext;
