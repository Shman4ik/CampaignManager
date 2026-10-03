using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Documents;
using CampaignManager.Core.Encounters;
using static CampaignManager.Core.Tests.Sheet.Sheets;

namespace CampaignManager.Core.Tests.Encounters;

/// <summary>
/// Ядро сцены (T2.6a): участник — ссылка плюс снимок, резолв возвращает эффекты, применяет их один <c>Apply</c>,
/// «Отменить» — выбросить эффекты. Против v1: атака меняла участников до «Применить», и «Отменить» не возвращало
/// ход, патроны и защиту цели (F-C02) — здесь до <c>Apply</c> не меняется ничего.
/// </summary>
public sealed class EncounterEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid DeepOneId = Guid.CreateVersion7();

    private static Statblock DeepOne() => new()
    {
        Str = new StatValue { Value = 85 },
        Con = new StatValue { Value = 50 },
        Siz = new StatValue { Value = 85 },
        Dex = new StatValue { Value = 50 },
        Int = new StatValue { Value = 65 },
        Pow = new StatValue { Value = 50 },
        HitPoints = 13,
        MagicPoints = 10,
        DamageBonus = "+1D4",
        Build = 1,
        Armor = 1,
        SanityLoss = "0/1D6",
    };

    /// <summary>Сыщик (ПЗ 14, ПМ 14, Рассудок 50) и Глубоководный.</summary>
    private static (EncounterState State, EncounterParticipant Investigator, EncounterParticipant Creature, CharacterSheet Sheet) Scene()
    {
        var state = new EncounterState();
        var sheet = NewSheet(50);
        sheet.Personal.Name = "Харви";
        sheet.Current.HitPoints = 14;
        sheet.Current.MagicPoints = 14;
        sheet.Current.Luck = 55;
        var investigator = EncounterParticipants.FromSheet(Guid.CreateVersion7(), CharacterKind.Player, sheet, Catalog);
        var creature = EncounterParticipants.FromStatblock(DeepOneId, "Глубоководный", DeepOne());
        EncounterEngine.Add(state, investigator, Now);
        EncounterEngine.Add(state, creature, Now);
        return (state, investigator, creature, sheet);
    }

    private static EncounterEffect Damage(Guid target, int amount) =>
        new() { Kind = EncounterEffectKind.Damage, ParticipantId = target, Amount = amount };

    [Fact]
    public void Snapshot_from_sheet_and_statblock()
    {
        var (_, investigator, creature, _) = Scene();

        Assert.Equal(ParticipantKind.Investigator, investigator.Kind);
        Assert.Equal(EncounterSide.Investigators, investigator.Side);
        Assert.Equal(14, investigator.MaxHitPoints); // (ТЕЛ 60 + ВЫН 80) / 10
        Assert.Equal(50, investigator.Sanity);
        Assert.Equal(45, investigator.Initiative); // ЛВК
        Assert.NotEqual(investigator.Id, investigator.SourceCharacterId); // id участника ≠ id листа

        Assert.Equal(ParticipantKind.Creature, creature.Kind);
        Assert.Equal(EncounterSide.Enemies, creature.Side);
        Assert.Null(creature.Sanity); // рассудка и Удачи у твари нет
        Assert.Null(creature.Luck);
        Assert.Equal(50, creature.Initiative); // инициатива 0 — по ЛВК
        Assert.Equal("0/1D6", creature.SanityLoss);
    }

    [Fact]
    public void One_sheet_joins_once_creatures_get_numbers()
    {
        var (state, investigator, _, sheet) = Scene();

        var again = EncounterParticipants.FromSheet(investigator.SourceCharacterId!.Value, CharacterKind.Player, sheet, Catalog);
        var rejected = EncounterEngine.Add(state, again, Now);
        var second = EncounterEngine.Add(state, EncounterParticipants.FromStatblock(DeepOneId, "Глубоководный", DeepOne()), Now);
        var third = EncounterEngine.Add(state, EncounterParticipants.FromStatblock(DeepOneId, "Глубоководный", DeepOne()), Now);

        Assert.NotNull(rejected.Rejection);
        Assert.Equal(["Харви", "Глубоководный #1", "Глубоководный #2", "Глубоководный #3"], state.Participants.Select(p => p.Name));
        Assert.Equal("Глубоководный", third.Participant!.SourceName);
        Assert.NotEqual(second.Participant!.Id, third.Participant.Id);
    }

    [Fact]
    [Trait("finding", "F-C02")]
    public void Proposal_and_preview_change_nothing_cancel_discards()
    {
        var (state, investigator, _, _) = Scene();
        var before = CmJson.Clone(state);
        var resolution = new EncounterResolution { Title = "Когти", Effects = [Damage(investigator.Id, 8)] };

        EncounterEngine.Propose(state, resolution);
        var preview = EncounterEngine.Preview(state, resolution);

        var line = Assert.Single(preview);
        Assert.Equal(("14", "6"), (line.Before, line.After));
        Assert.Contains("серьёзная рана", line.Note);
        Assert.Equal(14, investigator.HitPoints); // предпросмотр считал на копии

        EncounterEngine.Cancel(state);
        Assert.Equal(CmJson.Serialize(before), CmJson.Serialize(state));
    }

    [Fact]
    public void Round_counters_are_previewed_by_the_engine_but_never_reach_the_log()
    {
        var (state, investigator, creature, _) = Scene();
        var resolution = new EncounterResolution
        {
            Title = "Атака",
            Effects = [new EncounterEffect { Kind = EncounterEffectKind.Attack, ParticipantId = creature.Id, Amount = 1, Flag = true }, Damage(investigator.Id, 3)],
        };

        Assert.Contains(EncounterEngine.Preview(state, resolution), l => l.IsCounter);
        var outcome = EncounterEngine.Apply(state, resolution, Now);

        Assert.DoesNotContain(outcome.Entry.Lines, l => l.Contains("за раунд", StringComparison.Ordinal));
        Assert.Contains(outcome.Entry.Lines, l => l.Contains("Урон 3", StringComparison.Ordinal));
    }

    [Fact]
    public void Apply_changes_participants_logs_and_queues_sheet_writes()
    {
        var (state, investigator, creature, _) = Scene();
        var resolution = new EncounterResolution
        {
            Title = "Урон",
            Lines = ["Когти: 1d6+1d4 → 8"],
            Effects = [Damage(investigator.Id, 8), Damage(creature.Id, 5)],
        };
        EncounterEngine.Propose(state, resolution);

        var outcome = EncounterEngine.Apply(state, Now)!;

        Assert.Null(state.Pending);
        Assert.Equal(6, investigator.HitPoints);
        Assert.True(investigator.MajorWound); // 8 ≥ 7 = ⌈14 / 2⌉
        Assert.Equal(8, creature.HitPoints);
        Assert.Equal(EncounterLogKind.Effect, outcome.Entry.Kind);
        Assert.Equal("Когти: 1d6+1d4 → 8", outcome.Entry.Lines[0]);
        Assert.Contains(outcome.Entry.Lines, l => l.StartsWith("Глубоководный: Урон 5, 13 → 8", StringComparison.Ordinal));

        var write = Assert.Single(state.SheetWrites); // у твари листа нет
        Assert.Equal(investigator.SourceCharacterId, write.CharacterId);
        Assert.Equal(8, Assert.Single(write.Effects).Amount);
    }

    [Fact]
    [Trait("page", "118")]
    public void Damage_to_zero_with_major_wound_is_dying_without_is_unconscious()
    {
        var (state, investigator, creature, _) = Scene();

        EncounterEngine.Apply(state, new EncounterResolution { Effects = [Damage(investigator.Id, 9), Damage(investigator.Id, 9)] }, Now);
        EncounterEngine.Apply(state, new EncounterResolution { Effects = [Damage(creature.Id, 3), Damage(creature.Id, 3), Damage(creature.Id, 3), Damage(creature.Id, 4)] }, Now);

        Assert.Equal(0, investigator.HitPoints);
        Assert.True(investigator.Dying);
        Assert.True(investigator.Unconscious); // при смерти — и без сознания (стр. 118)
        Assert.Equal(0, creature.HitPoints);
        Assert.True(creature.Unconscious); // обычный урон: только без сознания
        Assert.False(creature.Dying);
    }

    [Fact]
    public void Sanity_loss_skips_creatures_and_out_effect_toggles_queue()
    {
        var (state, investigator, creature, _) = Scene();

        var preview = EncounterEngine.Preview(state, new EncounterResolution
        {
            Effects = [new EncounterEffect { Kind = EncounterEffectKind.SanityLoss, ParticipantId = creature.Id, Amount = 3 }],
        });
        EncounterEngine.Apply(state, new EncounterResolution
        {
            Effects =
            [
                new EncounterEffect { Kind = EncounterEffectKind.SanityLoss, ParticipantId = investigator.Id, Amount = 4 },
                new EncounterEffect { Kind = EncounterEffectKind.Out, ParticipantId = creature.Id, Flag = true },
            ],
        }, Now);

        Assert.Equal("у твари рассудка нет", preview[0].Note);
        Assert.Equal(46, investigator.Sanity);
        Assert.True(creature.IsOut);
        Assert.DoesNotContain(state.SheetWrites.SelectMany(w => w.Effects), e => e.Kind == EncounterEffectKind.Out);
    }

    [Fact]
    public void Completed_sheet_write_refreshes_snapshot_from_the_sheet()
    {
        var (state, investigator, _, sheet) = Scene();
        EncounterEngine.Apply(state, new EncounterResolution { Effects = [Damage(investigator.Id, 3)] }, Now);
        var write = Assert.Single(state.SheetWrites);

        var lines = EncounterSheetEffects.Apply(sheet, Catalog, write.Effects);
        sheet.Current.Luck = 40; // игрок между делом потратил Удачу
        EncounterEngine.CompleteSheetWrite(state, write.Id, sheet, Catalog, lines, Now);

        Assert.Empty(state.SheetWrites);
        Assert.Equal(11, investigator.HitPoints);
        Assert.Equal(40, investigator.Luck);
        Assert.Equal(EncounterLogKind.Sheet, state.Log[^1].Kind);
        Assert.Equal("ПЗ 14 → 11", state.Log[^1].Lines[0]);
    }

    [Fact]
    public void Failed_sheet_write_stays_queued_with_error_and_can_be_dropped()
    {
        var (state, investigator, _, _) = Scene();
        EncounterEngine.Apply(state, new EncounterResolution { Title = "Падение", Effects = [Damage(investigator.Id, 2)] }, Now);
        var write = state.SheetWrites[0];

        EncounterEngine.FailSheetWrite(state, write.Id, "Нет связи");
        Assert.Equal("Нет связи", state.SheetWrites[0].Error);

        EncounterEngine.DropSheetWrite(state, write.Id, Now);
        Assert.Empty(state.SheetWrites);
        Assert.Contains("Падение", state.Log[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Log_keeps_the_last_entries_with_round_numbers()
    {
        var state = new EncounterState { Round = 3 };
        for (var i = 0; i < EncounterEngine.MaxLogEntries + 10; i++)
            EncounterEngine.Note(state, $"запись {i}", Now);

        Assert.Equal(EncounterEngine.MaxLogEntries, state.Log.Count);
        Assert.Equal("запись 10", state.Log[0].Text);
        Assert.All(state.Log, e => Assert.Equal(3, e.Round));
    }

    [Fact]
    public void State_with_pending_and_sheet_writes_round_trips_with_enum_names()
    {
        var (state, investigator, _, _) = Scene();
        EncounterEngine.Apply(state, new EncounterResolution { Effects = [Damage(investigator.Id, 2)] }, Now);
        EncounterEngine.Propose(state, new EncounterResolution
        {
            Kind = EncounterLogKind.Effect,
            Effects = [new EncounterEffect { Kind = EncounterEffectKind.SanityLoss, ParticipantId = investigator.Id, Amount = 1, CreatureName = "Глубоководный" }],
        });

        using var json = CmJson.Write(state);
        var read = CmJson.ReadEncounterState(json, EncounterState.CurrentVersion);

        Assert.Equal("SanityLoss", json.RootElement.GetProperty("pending").GetProperty("effects")[0].GetProperty("kind").GetString());
        Assert.Equal("Damage", json.RootElement.GetProperty("sheetWrites")[0].GetProperty("effects")[0].GetProperty("kind").GetString());
        Assert.Equal(CmJson.Serialize(state), CmJson.Serialize(read));
    }

    [Fact]
    public void Every_log_kind_and_effect_kind_has_a_label()
    {
        foreach (var kind in Enum.GetValues<EncounterLogKind>())
        {
            Assert.NotEqual(kind.ToString(), EncounterText.Of(kind));
            _ = EncounterText.Category(kind);
        }

        foreach (var kind in Enum.GetValues<EncounterEffectKind>())
            Assert.NotEqual(kind.ToString(), EncounterText.Of(kind));
    }
}
