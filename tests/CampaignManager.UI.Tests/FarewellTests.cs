using Bunit;
using CampaignManager.Contracts.Campaigns;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Files;
using CampaignManager.Core.Characters;
using CampaignManager.UI.Characters;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Гибель и выбывание сыщика (гл. 10): «Проводить сыщика» — эпилог и, если журнал правит Хранитель, абзац в последнюю встречу;
/// графа «Эпилог» в биографии — только у выбывшего.
/// </summary>
public sealed class FarewellTests : KitContext
{
    private static readonly Guid CampaignId = Guid.Parse("0199b000-0000-7000-8000-000000000300");

    private static CampaignSessionDto Session(int number, string title) =>
        new(Guid.NewGuid(), number, new DateOnly(2026, 10, number), title, "Хроника.", null, null, null, null, false);

    private void Journal(bool canEdit) =>
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()
        {
            [nameof(ICampaignsApi.GetJournalAsync)] = _ => Task.FromResult(new CampaignJournalDto(
                CampaignId, "Маски", canEdit, 4, [Session(2, "Подвал"), Session(3, "Озеро")], [], [])),
        }));

    private IRenderedComponent<FarewellModal> Modal(List<FarewellRequest> requests) =>
        Render<FarewellModal>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.Name, "Харви Уолтерс")
            .Add(c => c.Fate, InvestigatorFate.Dead)
            .Add(c => c.CampaignId, CampaignId)
            .Add(c => c.OnConfirm, requests.Add));

    [Fact]
    public void Keeper_gets_the_last_session_for_the_epilogue()
    {
        Journal(canEdit: true);
        List<FarewellRequest> requests = [];
        var cut = Modal(requests);

        cut.WaitForAssertion(() => Assert.Contains("встреча №3 «Озеро»", cut.Markup));
        Assert.Contains("погиб", cut.Find("[data-testid=farewell-modal]").TextContent);
        cut.Find("[data-testid=farewell-epilogue]").Input("Тело так и не нашли.");
        cut.Find("[data-testid=farewell-confirm]").Click();

        var request = Assert.Single(requests);
        Assert.Equal("Тело так и не нашли.", request.Epilogue);
        Assert.Equal(3, request.Session?.Number);
    }

    [Fact]
    public void Player_without_journal_rights_keeps_the_epilogue_on_the_sheet()
    {
        Journal(canEdit: false);
        List<FarewellRequest> requests = [];
        var cut = Modal(requests);

        cut.Find("[data-testid=farewell-confirm]").Click();

        Assert.Empty(cut.FindAll("[data-testid=farewell-journal]"));
        Assert.Null(Assert.Single(requests).Session);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Epilogue_field_only_for_an_investigator_out_of_play(bool dead, bool shown)
    {
        Services.AddSingleton(Fake.Of<IFilesApi>(new()));
        var sheet = new CharacterSheet { Characteristics = new Characteristics { Pow = 50 }, Current = new CurrentValues { Sanity = 40 } };
        sheet.Condition.Dead = dead;
        var context = new SheetContext(new CharacterDto { Sheet = sheet, CanEdit = true, Kind = CharacterKind.Player }, new SkillCatalog([]), [], null!, () => { }, _ => { });

        var cut = Render<CascadingValue<SheetContext>>(p => p.Add(c => c.Value, context).AddChildContent<BiographyPanel>());

        Assert.Equal(shown, cut.FindAll("[data-testid=bio-epilogue]").Count == 1);
    }
}
