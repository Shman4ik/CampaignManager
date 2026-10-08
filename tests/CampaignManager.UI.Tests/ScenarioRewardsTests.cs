using Bunit;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Scenarios;
using CampaignManager.UI.Checks;
using CampaignManager.UI.Scenarios;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Итоги сценария для партии: одна награда — всем отмеченным сыщикам, «было → станет» до записи, в лист — через API с версией;
/// мёртвый не отмечен, записанный второй раз не пишется.
/// </summary>
public sealed class ScenarioRewardsTests : KitContext
{
    private readonly Dictionary<Guid, CharacterSheet> _sheets = [];
    private readonly List<Guid> _saved = [];

    public ScenarioRewardsTests() =>
        Services.AddSingleton(Fake.Of<ICharactersApi>(new()
        {
            [nameof(ICharactersApi.GetAsync)] = args => Task.FromResult(new CharacterDto
            {
                Id = (Guid)args![0]!, Version = 7, Sheet = CampaignManager.Core.Documents.CmJson.Clone(_sheets[(Guid)args[0]!]),
            }),
            [nameof(ICharactersApi.SaveSheetAsync)] = args =>
            {
                _sheets[(Guid)args![0]!] = (CharacterSheet)args[1]!;
                _saved.Add((Guid)args[0]!);
                return Task.FromResult(new CharacterSavedDto(8, DateTimeOffset.UnixEpoch));
            },
        }));

    private CheckInvestigator Investigator(string name, int sanity, bool dead = false)
    {
        var sheet = new CharacterSheet
        {
            Personal = new PersonalInfo { Name = name },
            Characteristics = new Characteristics { Pow = 70, Int = 60 },
            Current = new CurrentValues { Sanity = sanity },
            Condition = new SheetCondition { Dead = dead },
        };
        var id = Guid.NewGuid();
        _sheets[id] = sheet;
        return new CheckInvestigator(id, name, sheet);
    }

    private IRenderedComponent<ScenarioRewardsModal> Modal(params CheckInvestigator[] party) =>
        Render<ScenarioRewardsModal>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.Party, party)
            .Add(c => c.Catalog, new SkillCatalog([]))
            .Add(c => c.Rewards, [new KeyFactDto(Guid.NewGuid(), 0, KeyFactType.Reward, "Спасли пленников", "По 1d6 Рассудка.")]));

    [Fact]
    public void Sanity_gain_for_everyone_alive_with_preview_then_written_to_sheets()
    {
        var harvey = Investigator("Харви", 40);
        var roger = Investigator("Роджер", 50);
        var cecil = Investigator("Сесил", 30, dead: true);
        var cut = Modal(harvey, roger, cecil);

        Assert.Contains("Спасли пленников", cut.Find("[data-testid=rewards-facts]").TextContent);
        Assert.True(cut.Find("[data-testid=rewards-apply]").HasAttribute("disabled")); // кости не брошены

        var points = cut.FindAll("[data-testid=rewards-points]");
        Assert.Equal(2, points.Count); // мёртвый не отмечен
        points[0].Change("4");
        cut.FindAll("[data-testid=rewards-points]")[1].Change("6");
        Assert.Equal(["→ Рассудок 40 → 44", "→ Рассудок 50 → 56"], cut.FindAll("[data-testid=rewards-preview]").Select(p => p.TextContent.Trim()));

        cut.Find("[data-testid=rewards-apply]").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, _saved.Count));
        Assert.Equal(44, _sheets[harvey.Id].Current.Sanity);
        Assert.Equal(56, _sheets[roger.Id].Current.Sanity);
        Assert.Equal(30, _sheets[cecil.Id].Current.Sanity);
    }

    [Fact]
    public void Sanity_check_takes_success_or_failure_part_and_fumble_takes_maximum()
    {
        var harvey = Investigator("Харви", 40);
        var roger = Investigator("Роджер", 40);
        var cut = Modal(harvey, roger);

        cut.FindAll("button.cm-segment").Single(b => b.TextContent.Trim() == "Проверка Рассудка").Click();
        var rolls = cut.FindAll("input[aria-label^='Рассудок:']");
        rolls[0].Change("20"); // успех — 1
        cut.FindAll("input[aria-label^='Рассудок:']")[1].Change("100"); // крах — максимум 1d6 = 6

        Assert.Equal(["→ Рассудок 40 → 39", "→ Рассудок 40 → 34"], cut.FindAll("[data-testid=rewards-preview]").Select(p => p.TextContent.Trim()));
        cut.Find("[data-testid=rewards-apply]").Click();

        cut.WaitForAssertion(() => Assert.Equal(34, _sheets[roger.Id].Current.Sanity));
        Assert.Equal(6, _sheets[roger.Id].Condition.LastSanityLoss);
    }

    [Fact]
    public void Money_goes_to_cash_of_each()
    {
        var harvey = Investigator("Харви", 40);
        var cut = Modal(harvey);

        cut.FindAll("button.cm-segment").Single(b => b.TextContent.Trim() == "Деньги").Click();
        cut.Find("[data-testid=rewards-money]").Change("250");
        cut.Find("[data-testid=rewards-apply]").Click();

        cut.WaitForAssertion(() => Assert.Equal(250m, _sheets[harvey.Id].Finances.Cash));
    }
}
