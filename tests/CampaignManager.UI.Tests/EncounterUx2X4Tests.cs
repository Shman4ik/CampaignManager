using Bunit;
using CampaignManager.Contracts.Encounters;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Encounters;
using CampaignManager.UI.Encounters;
using CampaignManager.UI.Encounters.Combat;
using CampaignManager.UI.Platform;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>UX-2 (X4): вкладки боя — раненые кнопками, кости участника выбором, настройки сцены не во вкладке «Прочее».</summary>
public sealed class EncounterUx2X4Tests : KitContext
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private (EncounterSession Session, EncounterParticipant Investigator) Scene()
    {
        var state = new EncounterState();
        var investigator = new EncounterParticipant
        {
            Name = "Харви", Kind = ParticipantKind.Npc, Initiative = 60, Side = EncounterSide.Investigators, HitPoints = 6, MaxHitPoints = 12,
            Stats = new ParticipantStats { Con = 50, Dex = 60, Dodge = 30 },
            Profile = new CombatProfile
            {
                FirstAid = 60,
                Attacks = [new CombatAttack { Key = "brawl", Name = "Драка", Skill = 50, Damage = "1D3" }],
            },
        };
        var ghoul = EncounterParticipants.FromStatblock(null, "Гуль", new Statblock { HitPoints = 13, Dex = new StatValue { Value = 40 }, Con = new StatValue { Value = 50 } });
        EncounterEngine.Add(state, ghoul, Now);
        EncounterEngine.Add(state, investigator, Now);
        EncounterQueue.Start(state, Now);

        var session = new EncounterSession(new CombatPanelTests.SavingEncounters(), new EncounterSheetSync(new CombatPanelTests.NoCharacters()),
            Services.GetRequiredService<BrowserStorage>(), Time);
        session.Start(new EncounterDto { Id = Guid.CreateVersion7(), State = state, Version = 1 }, new SkillCatalog([]));
        return (session, investigator);
    }

    private IRenderedComponent<CascadingValue<EncounterSession>> RenderPanel(EncounterSession session, string tab) =>
        Render<CascadingValue<EncounterSession>>(p => p
            .Add(c => c.Value, session)
            .Add(c => c.IsFixed, true)
            .AddChildContent<CombatPanel>(c => c.Add(x => x.Tab, tab)));

    [Fact]
    public void Wounds_tab_lists_the_wounded_as_buttons_and_picks_a_capable_healer()
    {
        var (session, _) = Scene();
        // Ход у гуля: у него нет Первой помощи — лекарем по умолчанию встаёт тот, кто умеет.
        var cut = RenderPanel(session, "wounds");

        var wounded = cut.Find("[data-testid=wounded-list]");
        Assert.Contains("Харви", wounded.TextContent, StringComparison.Ordinal);
        Assert.Contains("ПЗ 6/12", wounded.TextContent, StringComparison.Ordinal);
        Assert.Equal("Харви", cut.Find("select[aria-label='Кто лечит'] option[selected]").TextContent.Trim());
    }

    [Fact]
    public void Dice_tab_takes_the_value_from_the_participant()
    {
        var (session, _) = Scene();
        var cut = RenderPanel(session, "dice");

        cut.Find("[data-testid=dice-target]").Change("con");

        // Значение подставлено из снимка участника — полей «Что проверяем» и «Значение» нет.
        Assert.Empty(cut.FindAll("[data-testid=check-value]"));
        Assert.Contains("ВЫН", cut.Find("[data-testid=check-subject-head]").TextContent, StringComparison.Ordinal);
        Assert.Contains("50%", cut.Find("[data-testid=check-subject-head]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Other_tab_has_no_scene_rules()
    {
        var (session, _) = Scene();
        var cut = RenderPanel(session, "actions");

        Assert.DoesNotContain("Необязательные правила", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Нокаут манёвром", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Maneuver_and_spell_tabs_say_why_the_button_is_off()
    {
        var (session, _) = Scene();
        var maneuver = RenderPanel(session, "maneuver");
        Assert.Contains("Цель", maneuver.Markup, StringComparison.Ordinal);
        Assert.Contains("maneuver-extra", maneuver.Markup, StringComparison.Ordinal);

        var spell = RenderPanel(session, "spell");
        Assert.True(spell.Find("[data-testid=spell-cast]").HasAttribute("disabled"));
        Assert.Contains("Не хватает: название заклинания", spell.Markup, StringComparison.Ordinal);
    }
}
