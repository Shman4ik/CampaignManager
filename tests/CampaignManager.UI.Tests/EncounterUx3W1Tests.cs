using Bunit;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Encounters;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Encounters;
using CampaignManager.UI.Encounters;
using CampaignManager.UI.Encounters.Combat;
using CampaignManager.UI.Platform;
using CampaignManager.UI.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>UX-3 (W1): сегментированный переключатель, «Чем» боя, окно участника, погоня (#213).</summary>
public sealed class EncounterUx3W1Tests : KitContext
{
    [Fact]
    public void Segmented_marks_choice_as_radio_not_as_primary_button()
    {
        var picked = "";
        var cut = Render<Segmented<string>>(p => p
            .Add(c => c.Options, [new SegmentOption<string>("a", "Уклонение"), new SegmentOption<string>("b", "Контратака")])
            .Add(c => c.Value, "a")
            .Add(c => c.ValueChanged, v => picked = v));

        var buttons = cut.FindAll("[role='radio']");
        Assert.Equal("true", buttons[0].GetAttribute("aria-checked"));
        Assert.Equal("false", buttons[1].GetAttribute("aria-checked"));
        Assert.DoesNotContain("cm-btn-primary", cut.Markup);

        buttons[1].Click();
        Assert.Equal("b", picked);
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Attack_with_lists_combat_skills_and_picked_weapon_goes_to_npc_snapshot_and_is_selected()
    {
        var handgun = new SkillDefinition(Guid.NewGuid(), "Стрельба (пистолет)") { Code = "skill.firearms.handgun", BaseValue = 20, Category = SkillCategory.CombatFirearms };
        var catalog = new SkillCatalog([handgun]);
        var state = new EncounterState();
        var npc = new EncounterParticipant
        {
            Name = "Полицейский", Kind = ParticipantKind.Npc, Initiative = 60, Side = EncounterSide.Investigators, HitPoints = 12, MaxHitPoints = 12,
            Stats = new ParticipantStats { Con = 50, Dex = 60, Str = 50 },
            Profile = new CombatProfile
            {
                Attacks = [new CombatAttack { Key = "brawl", Name = "Драка", Skill = 50, Damage = "1D3" }],
                Skills = [new CombatSkill { SkillId = handgun.Id, Name = handgun.Name, Value = 55 }],
            },
        };
        var ghoul = EncounterParticipants.FromStatblock(null, "Гуль", new Statblock { HitPoints = 13 });
        EncounterEngine.Add(state, npc, Now);
        EncounterEngine.Add(state, ghoul, Now);
        EncounterQueue.Start(state, Now);
        var colt = new WeaponDto { Id = Guid.NewGuid(), Name = "Кольт .45", SkillId = handgun.Id, SkillName = handgun.Name, Damage = "1d10+2", Range = "15 метров" };
        Services.AddSingleton(Fake.Of<ICatalogApi<WeaponDto>>(new()
        {
            [nameof(ICatalogApi<WeaponDto>.ListAsync)] = _ => Task.FromResult(new CatalogList<WeaponDto>([colt, new WeaponDto { Id = Guid.NewGuid(), Name = "Нож", Damage = "1d4" }], true)),
        }));
        var session = new EncounterSession(Fake.Of<IEncountersApi>(new()
        {
            [nameof(IEncountersApi.SaveStateAsync)] = args => Task.FromResult(new EncounterSavedDto((uint)args![2]! + 1, Now)),
        }), new EncounterSheetSync(Fake.Of<ICharactersApi>(new())), Services.GetRequiredService<BrowserStorage>(), Time);
        session.Start(new EncounterDto { Id = Guid.CreateVersion7(), State = state, Version = 1 }, catalog);

        var cut = Render<CascadingValue<EncounterSession>>(p => p.Add(c => c.Value, session).Add(c => c.IsFixed, true).AddChildContent<CombatPanel>());

        var options = cut.Find("[data-testid=attack-weapon]").QuerySelectorAll("option").Select(o => o.TextContent.Trim()).ToList();
        Assert.Contains(options, o => o.StartsWith("Стрельба (пистолет) — 55%", StringComparison.Ordinal));
        Assert.Equal("Подобрать оружие…", options[^1]);

        cut.Find("[data-testid=attack-weapon]").Change("skill:" + handgun.Id.ToString("N"));
        // Окно — только оружие этого навыка.
        var rows = cut.FindAll("[data-testid=picker-row]");
        Assert.Single(rows);
        rows[0].Click();
        cut.Find("[data-testid=picker-confirm]").Click();

        var picked = Assert.Single(npc.Profile.Attacks, a => a.Picked);
        Assert.Equal(55, picked.Skill);
        Assert.Equal(CombatAttackKind.Ranged, picked.Kind);
        cut.WaitForAssertion(() => Assert.Contains("Кольт .45 — 55%", cut.Find("[data-testid=attack-weapon] option[selected]").TextContent, StringComparison.Ordinal));
    }
}
