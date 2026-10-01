using CampaignManager.Core.Characters;
using static CampaignManager.Core.Tests.Sheet.Sheets;

namespace CampaignManager.Core.Tests.Sheet;

/// <summary>
/// Рассудок главы 8: максимум, пороги безумия, списание, приступ, Мифы. Перенесено из T0.2 без правки
/// ожиданий, кроме находки F-S05 и её следствия: Мифы берутся из справочника, поэтому прибавку есть куда
/// записать, даже если строки Мифов на листе ещё нет.
/// </summary>
public sealed class SanityRulesTests
{
    // ── Максимум и Мифы ─────────────────────────────────────────────────────

    [Theory]
    [Trait("page", "63, 153")]
    [InlineData(0, 99)]
    [InlineData(10, 89)]
    [InlineData(99, 0)]
    [InlineData(120, 0)] // Мифы выше 99 вписаны руками — максимум не уходит в минус
    public void ComputeMaxSanity_Is99MinusMythos(int mythos, int expected) =>
        Assert.Equal(expected, SanityRules.ComputeMaxSanity(WithMythos(mythos), Catalog));

    [Fact]
    [Trait("page", "63, 153")]
    public void ComputeMaxSanity_NoMythosRow_Is99()
    {
        var sheet = NewSheet();

        Assert.Equal(99, SanityRules.ComputeMaxSanity(sheet, Catalog));
        Assert.Equal(0, SanityRules.MythosValue(sheet, Catalog));
    }

    [Fact]
    [Trait("page", "153")]
    public void AddMythos_LowersMaxSanity_AndClampsCurrent()
    {
        var sheet = WithMythos(0, sanity: 90);

        var gained = SanityRules.AddMythos(sheet, Catalog, 15);

        Assert.Equal(15, gained);
        Assert.Equal(15, Find(sheet, Mythos).Value);
        Assert.Equal(84, SanityRules.ComputeMaxSanity(sheet, Catalog));
        Assert.Equal(84, sheet.Current.Sanity);
    }

    [Fact]
    [Trait("page", "153")]
    public void AddMythos_CapsAt99_ReturnsActualGain()
    {
        var sheet = WithMythos(95, sanity: 4);

        Assert.Equal(4, SanityRules.AddMythos(sheet, Catalog, 10));
        Assert.Equal(99, Find(sheet, Mythos).Value);
        Assert.Equal(0, sheet.Current.Sanity);
    }

    [Theory]
    [Trait("page", "153")]
    [InlineData(0)]
    [InlineData(-3)]
    public void AddMythos_NonPositiveGain_ChangesNothing(int gain)
    {
        var sheet = WithMythos(10, sanity: 60);

        Assert.Equal(0, SanityRules.AddMythos(sheet, Catalog, gain));
        Assert.Equal(10, Find(sheet, Mythos).Value);
    }

    /// <summary>
    /// В v1 без строки Мифов на листе прибавка терялась (0). В 2.0 навык берётся из справочника: строка
    /// заводится. Ноль — только если Мифов нет и в справочнике.
    /// </summary>
    [Fact]
    [Trait("page", "153")]
    [Trait("finding", "F-S05")]
    public void AddMythos_NoMythosRow_CreatesIt_NoMythosInCatalog_ReturnsZero()
    {
        var sheet = NewSheet(sanity: 60);
        Assert.Equal(5, SanityRules.AddMythos(sheet, Catalog, 5));
        Assert.Equal(5, Find(sheet, Mythos).Value);

        var bare = NewSheet(sanity: 60);
        Assert.Equal(0, SanityRules.AddMythos(bare, CatalogWithoutMythosAndCredit, 5));
        Assert.Empty(bare.Skills);
    }

    [Fact]
    [Trait("page", "160-161")]
    public void RecordMythosInsanity_FirstGivesFive_NextGiveOne()
    {
        var sheet = WithMythos(0, sanity: 99);

        Assert.Equal(5, SanityRules.RecordMythosInsanity(sheet, Catalog));
        Assert.Equal(1, SanityRules.RecordMythosInsanity(sheet, Catalog));
        Assert.Equal(1, SanityRules.RecordMythosInsanity(sheet, Catalog));

        Assert.Equal(7, Find(sheet, Mythos).Value);
        Assert.Equal(3, sheet.Condition.MythosInsanityCount);
        Assert.Equal(92, SanityRules.ComputeMaxSanity(sheet, Catalog));
        Assert.Equal(92, sheet.Current.Sanity);
    }

    /// <summary>
    /// F-S05 исправлена: в v1 без навыка Мифов счётчик случаев рос и метод сообщал +5, хотя записать было
    /// некуда, и следующий случай давал уже +1. Теперь прибавка всегда записывается (строка заводится по
    /// справочнику), а без Мифов в справочнике случай не засчитывается.
    /// </summary>
    [Fact]
    [Trait("page", "160-161")]
    [Trait("finding", "F-S05")]
    public void RecordMythosInsanity_ReportedGainIsWhatWasRecorded()
    {
        var sheet = NewSheet(sanity: 60);
        Assert.Equal(5, SanityRules.RecordMythosInsanity(sheet, Catalog));
        Assert.Equal(5, SanityRules.MythosValue(sheet, Catalog));

        var bare = NewSheet(sanity: 60);
        Assert.Equal(0, SanityRules.RecordMythosInsanity(bare, CatalogWithoutMythosAndCredit));
        Assert.Equal(0, bare.Condition.MythosInsanityCount);
    }

    [Fact]
    [Trait("page", "160-161")]
    public void UndoMythosInsanity_RemovesLastGain_NextGainFollowsCount()
    {
        var sheet = WithMythos(0, sanity: 99);
        SanityRules.RecordMythosInsanity(sheet, Catalog);
        SanityRules.RecordMythosInsanity(sheet, Catalog);

        Assert.Equal(1, SanityRules.NextMythosGain(sheet));
        Assert.Equal(1, SanityRules.UndoMythosInsanity(sheet, Catalog));
        Assert.Equal(5, SanityRules.UndoMythosInsanity(sheet, Catalog));
        Assert.Equal(0, SanityRules.UndoMythosInsanity(sheet, Catalog));
        Assert.Equal(0, Find(sheet, Mythos).Value);
        Assert.Equal(5, SanityRules.NextMythosGain(sheet));
    }

    // ── Порог бессрочного безумия ───────────────────────────────────────────

    [Theory]
    [Trait("page", "154")]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    [InlineData(50, 10)]
    [InlineData(51, 11)]
    [InlineData(52, 11)] // 10,4 → 11, а не 10
    [InlineData(55, 11)]
    [InlineData(56, 12)]
    [InlineData(99, 20)]
    public void IndefiniteInsanityThreshold_FifthRoundedUp(int sanityAtDayStart, int expected) =>
        Assert.Equal(expected, SanityRules.IndefiniteInsanityThreshold(sanityAtDayStart));

    [Theory]
    [Trait("page", "154")]
    [InlineData(40, 12, 52)]
    [InlineData(0, 7, 7)]
    [InlineData(-3, 5, 5)]
    [InlineData(40, -2, 40)]
    public void SanityAtDayStart_CurrentPlusLostToday_NegativesIgnored(int current, int lost, int expected) =>
        Assert.Equal(expected, SanityRules.SanityAtDayStart(current, lost));

    [Theory]
    [Trait("page", "154")]
    [InlineData(42, 10, false)] // утром 52, нужно 11
    [InlineData(41, 11, true)]
    [InlineData(40, 10, true)] // утром 50, нужно 10
    [InlineData(40, 0, false)]
    [InlineData(0, 0, false)]
    [InlineData(0, 3, true)] // утром 3 — порог 1
    public void IsIndefiniteInsanityLoss_ComparesWithFifthOfMorningSanity(int current, int lost, bool expected) =>
        Assert.Equal(expected, SanityRules.IsIndefiniteInsanityLoss(current, lost));

    [Fact]
    [Trait("page", "153")]
    public void TemporaryInsanityThreshold_IsFive() => Assert.Equal(5, SanityRules.TemporaryInsanityThreshold);

    [Theory]
    [Trait("page", "154")]
    [InlineData(1, false)]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    public void IsPermanentlyInsane_SanityAtOrBelowZero(int sanity, bool expected) =>
        Assert.Equal(expected, SanityRules.IsPermanentlyInsane(NewSheet(sanity)));

    // ── Списание: оба окна ──────────────────────────────────────────────────

    [Fact]
    [Trait("page", "153-154")]
    public void ApplyLoss_SingleCause_FillsBothWindows()
    {
        var sheet = NewSheet(sanity: 50);

        var lost = SanityRules.ApplyLoss(sheet, Catalog, 6);

        Assert.Equal(6, lost);
        Assert.Equal(44, sheet.Current.Sanity);
        Assert.Equal(6, sheet.Condition.LastSanityLoss);
        Assert.Equal(6, sheet.Condition.SanityLostToday);
        Assert.True(sheet.Condition.LastSanityLoss >= SanityRules.TemporaryInsanityThreshold);
        Assert.False(sheet.Condition.BoutDue);
    }

    /// <summary>Два провала по 3 — не проверка ИНТ, но за день это пятая часть Рассудка.</summary>
    [Fact]
    [Trait("page", "153-154")]
    public void ApplyLoss_TwoSmallLosses_NoIntCheck_ButDayWindowReachesFifth()
    {
        var sheet = NewSheet(sanity: 30);

        SanityRules.ApplyLoss(sheet, Catalog, 3);
        SanityRules.ApplyLoss(sheet, Catalog, 3);

        Assert.Equal(3, sheet.Condition.LastSanityLoss);
        Assert.True(sheet.Condition.LastSanityLoss < SanityRules.TemporaryInsanityThreshold);
        Assert.Equal(6, sheet.Condition.SanityLostToday);
        Assert.True(SanityRules.IsIndefiniteInsanityLoss(sheet.Current.Sanity, sheet.Condition.SanityLostToday));
    }

    [Fact]
    [Trait("page", "154")]
    public void ApplyLoss_MoreThanLeft_StopsAtZero()
    {
        var sheet = NewSheet(sanity: 3);

        Assert.Equal(3, SanityRules.ApplyLoss(sheet, Catalog, 10));
        Assert.Equal(0, sheet.Current.Sanity);
        Assert.Equal(3, sheet.Condition.LastSanityLoss);
    }

    [Theory]
    [Trait("page", "153")]
    [InlineData(0)]
    [InlineData(-4)]
    public void ApplyLoss_NonPositive_LosesNothing_ButResetsLastLoss(int amount)
    {
        var sheet = NewSheet(sanity: 40);
        sheet.Condition.LastSanityLoss = 7;
        sheet.Condition.SanityLostToday = 7;

        Assert.Equal(0, SanityRules.ApplyLoss(sheet, Catalog, amount));
        Assert.Equal(40, sheet.Current.Sanity);
        Assert.Equal(0, sheet.Condition.LastSanityLoss);
        Assert.Equal(7, sheet.Condition.SanityLostToday);
    }

    /// <summary>Текущий Рассудок выше максимума сначала прижимается к 99 − Мифы.</summary>
    [Fact]
    [Trait("page", "153")]
    public void ApplyLoss_CurrentAboveMax_ClampsBeforeLoss()
    {
        var sheet = WithMythos(20, sanity: 90);

        Assert.Equal(5, SanityRules.ApplyLoss(sheet, Catalog, 5));
        Assert.Equal(74, sheet.Current.Sanity);
    }

    [Theory]
    [Trait("page", "156")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ApplyLoss_AlreadyInsane_AnyLossMakesBoutDue(bool temporary, bool indefinite)
    {
        var sheet = NewSheet(sanity: 40);
        sheet.Condition.TemporaryInsanity = temporary;
        sheet.Condition.IndefiniteInsanity = indefinite;

        SanityRules.ApplyLoss(sheet, Catalog, 1);

        Assert.True(sheet.Condition.BoutDue);
    }

    [Fact]
    [Trait("page", "156")]
    public void ApplyLoss_InsaneButNothingLost_BoutNotDue()
    {
        var sheet = NewSheet(sanity: 40);
        sheet.Condition.TemporaryInsanity = true;

        SanityRules.ApplyLoss(sheet, Catalog, 0);

        Assert.False(sheet.Condition.BoutDue);
    }

    [Fact]
    [Trait("page", "153-154")]
    public void StartNewDay_ClearsBothWindows()
    {
        var sheet = NewSheet(sanity: 40);
        SanityRules.ApplyLoss(sheet, Catalog, 6);

        SanityRules.StartNewDay(sheet);

        Assert.Equal((0, 0), (sheet.Condition.SanityLostToday, sheet.Condition.LastSanityLoss));
    }

    // ── Отметки безумия и приступ ───────────────────────────────────────────

    [Fact]
    [Trait("page", "153-154")]
    public void SetTemporaryInsanity_On_MakesBoutDue_AndStartsClock()
    {
        var sheet = NewSheet();
        var now = new DateTimeOffset(2026, 10, 1, 20, 0, 0, TimeSpan.Zero);

        SanityRules.SetTemporaryInsanity(sheet, true, now);

        Assert.True(sheet.Condition.TemporaryInsanity);
        Assert.Equal(now, sheet.Condition.TemporaryInsanityStartedAt);
        Assert.True(sheet.Condition.BoutDue);
        Assert.True(SanityRules.IsInsane(sheet));
    }

    [Fact]
    [Trait("page", "154")]
    public void SetIndefiniteInsanity_On_MakesBoutDue_AndStartsClock()
    {
        var sheet = NewSheet();

        SanityRules.SetIndefiniteInsanity(sheet, true);

        Assert.True(sheet.Condition.IndefiniteInsanity);
        Assert.NotNull(sheet.Condition.IndefiniteInsanityStartedAt);
        Assert.True(sheet.Condition.BoutDue);
    }

    [Fact]
    [Trait("page", "154")]
    public void ClearingOneMark_WhileOtherRemains_KeepsBoutDue()
    {
        var sheet = NewSheet();
        SanityRules.SetTemporaryInsanity(sheet, true);
        SanityRules.SetIndefiniteInsanity(sheet, true);

        SanityRules.SetTemporaryInsanity(sheet, false);

        Assert.Null(sheet.Condition.TemporaryInsanityStartedAt);
        Assert.True(sheet.Condition.BoutDue);

        SanityRules.SetIndefiniteInsanity(sheet, false);

        Assert.False(sheet.Condition.BoutDue);
        Assert.False(SanityRules.IsInsane(sheet));
    }

    [Fact]
    [Trait("page", "155-157")]
    public void RecordBout_StoresResult_AndClearsBoutDue()
    {
        var sheet = NewSheet();
        SanityRules.SetTemporaryInsanity(sheet, true);

        SanityRules.RecordBout(sheet, InsanityBoutMode.Summary, 7, 4);

        Assert.False(sheet.Condition.BoutDue);
        Assert.NotNull(sheet.Condition.LastBout);
        Assert.Equal(InsanityBoutMode.Summary, sheet.Condition.LastBout.Mode);
        Assert.Equal(7, sheet.Condition.LastBout.Roll);
        Assert.Equal(4, sheet.Condition.LastBout.Duration);
        Assert.True(sheet.Condition.TemporaryInsanity); // приступ безумия не снимает
    }

    [Fact]
    [Trait("page", "154")]
    public void DismissBout_ClearsBoutDue_KeepsMarks()
    {
        var sheet = NewSheet();
        SanityRules.SetIndefiniteInsanity(sheet, true);

        SanityRules.DismissBout(sheet);

        Assert.False(sheet.Condition.BoutDue);
        Assert.True(sheet.Condition.IndefiniteInsanity);
    }

    [Theory]
    [Trait("page", "164-165")]
    [InlineData(0, 40, 5, 45, 5)]
    [InlineData(30, 60, 20, 69, 9)]
    [InlineData(0, 40, -5, 35, -5)] // отрицательную прибавку не отсекает
    public void Grant_CappedBy99MinusMythos(int mythos, int sanity, int amount, int expected, int actual)
    {
        var sheet = WithMythos(mythos, sanity);

        Assert.Equal(actual, SanityRules.Grant(sheet, Catalog, amount));
        Assert.Equal(expected, sheet.Current.Sanity);
    }
}
