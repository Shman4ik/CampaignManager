using Bunit;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Encounters;
using CampaignManager.UI.Encounters;
using CampaignManager.UI.Shared;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>Общий UI сцены (T2.6a): предпросмотр и «Отменить», выбор целей, строка участника, полоса запаса.</summary>
public sealed class EncounterComponentTests : KitContext
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private static (EncounterState State, EncounterParticipant Ghoul) Scene()
    {
        var state = new EncounterState();
        var ghoul = EncounterParticipants.FromStatblock(null, "Гуль", new Statblock { HitPoints = 13, Dex = new StatValue { Value = 65 } });
        EncounterEngine.Add(state, ghoul, Now);
        return (state, ghoul);
    }

    [Fact]
    public void Preview_shows_before_and_after_and_leaves_state_untouched()
    {
        var (state, ghoul) = Scene();
        var resolution = new EncounterResolution
        {
            Title = "Выстрел",
            Lines = ["1d10 → 8"],
            Effects = [new EncounterEffect { Kind = EncounterEffectKind.Damage, ParticipantId = ghoul.Id, Amount = 8 }],
        };
        var applied = false;
        var cancelled = false;

        var cut = Render<ResolutionPreview>(p => p
            .Add(c => c.State, state)
            .Add(c => c.Resolution, resolution)
            .Add(c => c.OnApply, () => applied = true)
            .Add(c => c.OnCancel, () => cancelled = true));

        // Три столбца «кто / что / было → станет»; «Чем чревато» отдельным столбцом нет, а пояснение, которого в заголовке результата
        // нет, стоит мелко под названием строки (B29).
        Assert.DoesNotContain("Чем чревато", cut.Markup, StringComparison.Ordinal);
        var cells = cut.FindAll("tbody td").Select(td => td.TextContent.Trim()).ToList();
        Assert.Equal(4, cells.Count);
        Assert.Equal("Гуль", cells[0]);
        Assert.StartsWith("Урон 8", cells[1], StringComparison.Ordinal);
        Assert.Contains("серьёзная рана, падает, нужна проверка ВЫН", cells[1], StringComparison.Ordinal);
        Assert.Equal(["13", "5"], cells[2..]);
        Assert.Equal(13, ghoul.HitPoints);
        Assert.DoesNotContain("Запишется в лист", cut.Markup, StringComparison.Ordinal); // у твари листа нет

        cut.Find("[data-testid=cancel]").Click();
        cut.Find("[data-testid=apply]").Click();
        Assert.True(cancelled);
        Assert.True(applied);
    }

    [Fact]
    public void Participant_select_toggles_targets_and_selects_all()
    {
        var (state, _) = Scene();
        EncounterEngine.Add(state, EncounterParticipants.FromStatblock(null, "Гуль", new Statblock { HitPoints = 13 }), Now);
        IReadOnlySet<Guid> selected = new HashSet<Guid>();

        var cut = Render<ParticipantSelect>(p => p
            .Add(c => c.Participants, state.Participants)
            .Add(c => c.Multiple, true)
            .Add(c => c.Values, selected)
            .Add(c => c.ValuesChanged, values => selected = values));

        cut.FindAll("[data-testid=participant-chip]")[1].Click();
        Assert.Equal([state.Participants[1].Id], selected);

        cut.Find("[data-testid=participant-all]").Click(); // «Все»
        Assert.Equal(2, selected.Count);
    }

    [Fact]
    public void Participant_row_shows_numbers_and_condition_in_words()
    {
        var (state, ghoul) = Scene();
        EncounterEngine.Apply(state, new EncounterResolution
        {
            // 13 за раз — уже мгновенная смерть (F-S02); 7 (серьёзная рана) и 6 — при смерти.
            Effects = [new EncounterEffect { Kind = EncounterEffectKind.Damage, ParticipantId = ghoul.Id, Amount = 7, Check = true }, new EncounterEffect { Kind = EncounterEffectKind.Damage, ParticipantId = ghoul.Id, Amount = 6 }],
        }, Now);

        var cut = Render<ParticipantRow>(p => p.Add(c => c.Participant, ghoul).Add(c => c.Active, true));

        Assert.Contains("0/13", cut.Find("[data-testid=stat-hp]").TextContent.Replace(" ", "", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("при смерти", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("encounter-row-active", cut.Find("[data-testid=participant-row]").ClassName, StringComparison.Ordinal);
    }

    [Fact]
    public void Extra_from_scenario_pack_says_its_sheet_is_not_written()
    {
        var (_, ghoul) = Scene();
        ghoul.Note = "Статист: урон в лист не записывается";

        var cut = Render<ParticipantRow>(p => p.Add(c => c.Participant, ghoul));

        Assert.Contains("Статист", cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(StatKind.HitPoints, 100, "bg-error-500")]
    [InlineData(StatKind.HitPoints, 50, "bg-warning-500")]
    [InlineData(StatKind.Sanity, 20, "bg-error-700")]
    [InlineData(StatKind.MagicPoints, 10, "bg-accent-500")] // ПМ не предупреждает
    public void Stat_bar_warns_on_hit_points_and_sanity_only(StatKind kind, int percent, string fill)
    {
        Assert.Contains(fill, StatBar.FillClass(kind, percent), StringComparison.Ordinal);
    }
}
