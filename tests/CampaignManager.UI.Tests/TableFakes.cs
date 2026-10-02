using CampaignManager.Contracts.Campaigns;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Characters;
using CampaignManager.Core;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Стол для страниц, которым нужны кампании, листы и справочник навыков: одна кампания Хранителя с двумя
/// сыщиками. Что не нужно тестам — бросает.
/// </summary>
internal static class TableFakes
{
    public static readonly Guid CampaignId = Guid.Parse("0199b000-0000-7000-8000-000000000001");
    public static readonly SkillDto SpotHidden = new() { Id = Guid.Parse("0199b000-0000-7000-8000-000000000010"), Name = "Внимание", Code = SkillCodes.FromName("Внимание"), BaseValue = 25, Category = SkillCategory.InformationGathering };

    public static CharacterSheet Sheet(string name, int spotHidden) => new()
    {
        Personal = new PersonalInfo { Name = name },
        Skills = [new SheetSkill { SkillId = SpotHidden.Id, Value = spotHidden }],
    };

    public sealed class Campaigns : ICampaignsApi
    {
        public Task<IReadOnlyList<CampaignSummaryDto>> GetCampaignsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CampaignSummaryDto>>(
            [
                new(CampaignId, "Маски", CampaignKind.Campaign, CampaignStatus.Active, Era.Classic, DateTimeOffset.UnixEpoch,
                    CampaignRole.Keeper, null, 2, CanEdit: true, CanDelete: true),
            ]);

        public Task<HomeDto> GetHomeAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CampaignDetailsDto> GetCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CampaignSummaryDto> CreateCampaignAsync(CampaignInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CampaignSummaryDto> UpdateCampaignAsync(Guid campaignId, CampaignInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CampaignDetailsDto> JoinAsync(Guid campaignId, JoinCampaignRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CampaignMemberDto> UpdateMemberAsync(Guid campaignId, Guid userId, UpdateMemberRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RemoveMemberAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CampaignJournalDto> GetJournalAsync(Guid campaignId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CampaignSessionDto> AddSessionAsync(Guid campaignId, CampaignSessionInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CampaignSessionDto> UpdateSessionAsync(Guid campaignId, Guid sessionId, CampaignSessionInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteSessionAsync(Guid campaignId, Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    public sealed class Characters : ICharactersApi
    {
        public Task<IReadOnlyList<InvestigatorDto>> GetCampaignInvestigatorsAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InvestigatorDto>>(
            [
                new(Guid.NewGuid(), "Харви Уолтерс", "Аня", Sheet("Харви Уолтерс", 65)),
                new(Guid.NewGuid(), "Нора Флинн", null, Sheet("Нора Флинн", 40)),
            ]);

        public Task<CharacterDto> GetAsync(Guid characterId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CharacterSavedDto> SaveSheetAsync(Guid characterId, CharacterSheet sheet, uint version, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CharacterSavedDto> SetPortraitAsync(Guid characterId, Guid? fileId, uint version, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CharacterSavedDto> SetStatusAsync(Guid characterId, CharacterStatus status, uint version, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PartyMemberDto>> GetPartyAsync(Guid characterId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    public sealed class Skills : ICatalogApi<SkillDto>
    {
        public CatalogRoute Route => CatalogsRoutes.Skills;

        public Task<CatalogList<SkillDto>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new CatalogList<SkillDto>([SpotHidden], CanEdit: false));

        public Task<SkillDto> CreateAsync(SkillDto item, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SkillDto> UpdateAsync(SkillDto item, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CatalogImportReport> ImportAsync(Stream file, bool overwrite, bool dryRun, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CatalogImportReport> SyncAsync(bool dryRun, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
