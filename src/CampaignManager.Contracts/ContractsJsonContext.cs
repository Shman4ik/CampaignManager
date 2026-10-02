using System.Text.Json;
using System.Text.Json.Serialization;
using CampaignManager.Contracts.Campaigns;
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
[JsonSerializable(typeof(HomeDto))]
[JsonSerializable(typeof(IReadOnlyList<CampaignSummaryDto>))]
[JsonSerializable(typeof(CampaignSummaryDto))]
[JsonSerializable(typeof(CampaignDetailsDto))]
[JsonSerializable(typeof(CampaignMemberDto))]
[JsonSerializable(typeof(CampaignInput))]
[JsonSerializable(typeof(JoinCampaignRequest))]
[JsonSerializable(typeof(UpdateMemberRequest))]
[JsonSerializable(typeof(CampaignJournalDto))]
[JsonSerializable(typeof(CampaignSessionDto))]
[JsonSerializable(typeof(CampaignSessionInput))]
public sealed partial class ContractsJsonContext : JsonSerializerContext;
