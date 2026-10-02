using CampaignManager.Core.Characters;
using static CampaignManager.Core.Tests.Sheet.Sheets;

namespace CampaignManager.Core.Tests.Sheet;

/// <summary>
/// Правила, которые в v1 жили в разметке панели рассудка и книг Мифов (AUDIT, «Правила в разметке»): исход
/// проверки ИНТ, состояние панели, привыкание к ужасному (гл. 8, «Привыкание к ужасному»), стадия книги руками.
/// </summary>
public sealed class SanityPanelRulesTests
{
    [Fact]
    [Trait("page", "153")]
    public void Int_check_success_marks_temporary_insanity_and_bout()
    {
        var sheet = NewSheet(50);
        SanityRules.ApplyLoss(sheet, Catalog, 6);

        Assert.True(SanityRules.Status(sheet, Catalog).NeedsIntCheck);
        SanityRules.ResolveIntCheck(sheet, succeeded: true);

        Assert.True(sheet.Condition.TemporaryInsanity);
        Assert.True(sheet.Condition.BoutDue);
        Assert.False(SanityRules.Status(sheet, Catalog).NeedsIntCheck);
        Assert.Equal(6, sheet.Condition.SanityLostToday); // дневное окно не трогается
    }

    [Fact]
    [Trait("page", "153")]
    public void Int_check_failure_only_clears_prompt()
    {
        var sheet = NewSheet(50);
        SanityRules.ApplyLoss(sheet, Catalog, 5);

        SanityRules.ResolveIntCheck(sheet, succeeded: false);

        Assert.False(sheet.Condition.TemporaryInsanity);
        Assert.False(sheet.Condition.BoutDue);
        Assert.Equal(0, sheet.Condition.LastSanityLoss);
    }

    [Fact]
    [Trait("page", "154")]
    public void Status_counts_threshold_from_sanity_at_day_start()
    {
        var sheet = WithMythos(9, sanity: 52);
        SanityRules.ApplyLoss(sheet, Catalog, 4);
        SanityRules.ApplyLoss(sheet, Catalog, 4);

        var status = SanityRules.Status(sheet, Catalog);

        Assert.Equal(44, status.Current);
        Assert.Equal(90, status.Max);
        Assert.Equal(9, status.Mythos);
        Assert.Equal(11, status.IndefiniteThreshold); // ⅕ от 52, вверх
        Assert.False(status.IndefiniteLoss);
        Assert.False(status.NeedsIntCheck); // два раза по 4 — не одна причина
        Assert.Equal(48, status.Percent);
    }

    [Fact]
    [Trait("page", "154")]
    public void Bout_reminder_is_shown_only_while_insane()
    {
        var sheet = NewSheet(50);
        SanityRules.SetTemporaryInsanity(sheet, true);
        Assert.True(SanityRules.Status(sheet, Catalog).BoutDue);

        // Отметку сняли мимо SanityRules (чекбокс) — напоминать не о чем
        sheet.Condition.TemporaryInsanity = false;
        Assert.False(SanityRules.Status(sheet, Catalog).BoutDue);
    }

    [Fact]
    [Trait("page", "167")]
    public void Habituation_caps_loss_at_failure_maximum()
    {
        var sheet = NewSheet(60);
        var deepOnes = HabituationRules.Add(sheet, "Глубоководные", sanityLoss: "0/1d6");

        Assert.Equal(6, deepOnes.MaxLoss);
        Assert.Equal(4, HabituationRules.Lose(sheet, Catalog, deepOnes, 4));
        Assert.Equal(2, HabituationRules.Lose(sheet, Catalog, deepOnes, 5)); // осталось только 2
        Assert.Equal(0, HabituationRules.Lose(sheet, Catalog, deepOnes, 3));

        Assert.True(deepOnes.IsHabituated);
        Assert.Equal(54, sheet.Current.Sanity);
        Assert.Equal(2, sheet.Condition.LastSanityLoss); // каждая встреча — своя причина
    }

    [Fact]
    [Trait("page", "167")]
    public void Habituation_same_kind_does_not_create_second_counter()
    {
        var sheet = NewSheet();
        var manual = HabituationRules.Add(sheet, "Гули");
        var creatureId = Guid.NewGuid();

        var again = HabituationRules.Add(sheet, " гули ", creatureId, "0/1d6");

        Assert.Same(manual, again);
        Assert.Single(sheet.Condition.Habituations);
        Assert.Equal(creatureId, again.CreatureId);
        Assert.Equal(6, again.MaxLoss);

        HabituationRules.SetMaxLoss(again, 150);
        Assert.Equal(99, again.MaxLoss);
    }

    [Fact]
    [Trait("page", "167")]
    public void Habituation_without_limit_does_not_cap()
    {
        var sheet = NewSheet(30);
        var unknown = HabituationRules.Add(sheet, "Нечто");

        Assert.Equal(10, HabituationRules.Lose(sheet, Catalog, unknown, 10));
        Assert.Equal(20, sheet.Current.Sanity);
    }

    [Fact]
    [Trait("page", "173")]
    public void Book_stage_set_by_hand_has_no_effects()
    {
        var sheet = WithMythos(5, sanity: 50);
        var book = new MythosBookRecord { Name = "Некрономикон", MythosInitial = 5, MythosFull = 10, MythosRating = 30 };
        sheet.MythosBooks.Add(book);

        MythosBookRules.SetStage(book, MythosBookStage.FullStudy);
        Assert.Equal(1, book.FullStudyCount);
        MythosBookRules.SetStage(book, MythosBookStage.InitialReading);
        Assert.Equal(0, book.FullStudyCount);

        Assert.Equal(50, sheet.Current.Sanity);
        Assert.Equal(5, SanityRules.MythosValue(sheet, Catalog));
        Assert.Empty(book.Readings);
    }
}
