using System.Text.Json;
using System.Text.Json.Serialization;
using CampaignManager.Contracts.Platform;

namespace CampaignManager.Contracts;

/// <summary>
/// Единственный способ сериализовать DTO API: сгенерированный контекст вместо рефлексии,
/// чтобы клиент работал и под AOT (мобильное приложение, D4). Новый DTO модуля
/// добавляется сюда атрибутом. Сервер ставит этот контекст первым в цепочку резолверов.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(PingResponse))]
public sealed partial class ContractsJsonContext : JsonSerializerContext;
