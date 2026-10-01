using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Characters.Services;
using static CampaignManager.Rules.Tests.Sheet.Sheets;

namespace CampaignManager.Rules.Tests.Sheet;

/// <summary>Рассудок главы 8: максимум, пороги безумия, списание, приступ, Мифы.</summary>
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
        Assert.Equal(expected, SanityRules.ComputeMaxSanity(WithMythos(mythos)));

    [Fact]
    [Trait("page", "63, 153")]
    public void ComputeMaxSanity_NoMythosSkill_Is99()
    {
        var character = Character();

        Assert.Equal(99, SanityRules.ComputeMaxSanity(character));
        Assert.Equal(0, SanityRules.GetMythosValue(character));
        Assert.False(SanityRules.HasMythosSkill(character));
        Assert.True(SanityRules.HasMythosSkill(WithMythos(0)));
    }

    [Fact]
    [Trait("page", "153")]
    public void AddMythos_LowersMaxSanity_AndClampsCurrent()
    {
        var character = WithMythos(0, sanity: 90);

        var gained = SanityRules.AddMythos(character, 15);

        Assert.Equal(15, gained);
        Assert.Equal(15, Find(character, Mythos).Value.Regular);
        Assert.Equal(3, Find(character, Mythos).Value.Fifth);
        Assert.Equal(84, character.DerivedAttributes.Sanity.MaxValue);
        Assert.Equal(84, character.DerivedAttributes.Sanity.Value);
    }

    [Fact]
    [Trait("page", "153")]
    public void AddMythos_CapsAt99_ReturnsActualGain()
    {
        var character = WithMythos(95, sanity: 4);

        Assert.Equal(4, SanityRules.AddMythos(character, 10));
        Assert.Equal(99, Find(character, Mythos).Value.Regular);
        Assert.Equal(0, character.DerivedAttributes.Sanity.Value);
    }

    [Theory]
    [Trait("page", "153")]
    [InlineData(0)]
    [InlineData(-3)]
    public void AddMythos_NonPositiveGain_ChangesNothing(int gain)
    {
        var character = WithMythos(10, sanity: 60);

        Assert.Equal(0, SanityRules.AddMythos(character, gain));
        Assert.Equal(10, Find(character, Mythos).Value.Regular);
    }

    [Fact]
    [Trait("page", "153")]
    public void AddMythos_NoMythosSkill_ReturnsZero() =>
        Assert.Equal(0, SanityRules.AddMythos(Character(), 5));

    [Fact]
    [Trait("page", "160-161")]
    public void RecordMythosInsanity_FirstGivesFive_NextGiveOne()
    {
        var character = WithMythos(0, sanity: 99);

        Assert.Equal(5, SanityRules.RecordMythosInsanity(character));
        Assert.Equal(1, SanityRules.RecordMythosInsanity(character));
        Assert.Equal(1, SanityRules.RecordMythosInsanity(character));

        Assert.Equal(7, Find(character, Mythos).Value.Regular);
        Assert.Equal(3, character.State.MythosInsanityCount);
        Assert.Equal(92, character.DerivedAttributes.Sanity.MaxValue);
        Assert.Equal(92, character.DerivedAttributes.Sanity.Value);
    }

    /// <summary>
    ///     Без навыка Мифов прибавлять некуда, но счётчик случаев всё равно растёт и метод
    ///     возвращает 5 — следующий случай даст уже +1, хотя первые +5 никуда не легли.
    /// </summary>
    [Fact]
    [Trait("page", "160-161")]
    [Trait("finding", "F-S05")]
    public void RecordMythosInsanity_NoMythosSkill_CountsCaseAndReportsGainAnyway()
    {
        var character = Character(sanity: 60);

        Assert.Equal(5, SanityRules.RecordMythosInsanity(character));
        Assert.Equal(1, character.State.MythosInsanityCount);
        Assert.Equal(0, SanityRules.GetMythosValue(character));
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
    public void TemporaryInsanityThreshold_IsFive() =>
        Assert.Equal(5, SanityRules.TemporaryInsanityThreshold);

    [Theory]
    [Trait("page", "154")]
    [InlineData(1, false)]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    public void IsPermanentlyInsane_SanityAtOrBelowZero(int sanity, bool expected) =>
        Assert.Equal(expected, SanityRules.IsPermanentlyInsane(Character(sanity)));

    // ── Списание: оба окна ──────────────────────────────────────────────────

    [Fact]
    [Trait("page", "153-154")]
    public void ApplyLoss_SingleCause_FillsBothWindows()
    {
        var character = Character(sanity: 50);

        var lost = SanityRules.ApplyLoss(character, 6);

        Assert.Equal(6, lost);
        Assert.Equal(44, character.DerivedAttributes.Sanity.Value);
        Assert.Equal(6, character.State.LastSanityLoss);
        Assert.Equal(6, character.State.SanityLossEpisode);
        Assert.True(character.State.LastSanityLoss >= SanityRules.TemporaryInsanityThreshold);
        Assert.False(character.State.InsanityBoutDue);
    }

    /// <summary>Два провала по 3 — не проверка ИНТ, но за день это пятая часть Рассудка.</summary>
    [Fact]
    [Trait("page", "153-154")]
    public void ApplyLoss_TwoSmallLosses_NoIntCheck_ButDayWindowReachesFifth()
    {
        var character = Character(sanity: 30);

        SanityRules.ApplyLoss(character, 3);
        SanityRules.ApplyLoss(character, 3);

        Assert.Equal(3, character.State.LastSanityLoss);
        Assert.True(character.State.LastSanityLoss < SanityRules.TemporaryInsanityThreshold);
        Assert.Equal(6, character.State.SanityLossEpisode);
        Assert.True(SanityRules.IsIndefiniteInsanityLoss(
            character.DerivedAttributes.Sanity.Value, character.State.SanityLossEpisode));
    }

    [Fact]
    [Trait("page", "154")]
    public void ApplyLoss_MoreThanLeft_StopsAtZero()
    {
        var character = Character(sanity: 3);

        Assert.Equal(3, SanityRules.ApplyLoss(character, 10));
        Assert.Equal(0, character.DerivedAttributes.Sanity.Value);
        Assert.Equal(3, character.State.LastSanityLoss);
    }

    [Theory]
    [Trait("page", "153")]
    [InlineData(0)]
    [InlineData(-4)]
    public void ApplyLoss_NonPositive_LosesNothing_ButResetsLastLoss(int amount)
    {
        var character = Character(sanity: 40);
        character.State.LastSanityLoss = 7;
        character.State.SanityLossEpisode = 7;

        Assert.Equal(0, SanityRules.ApplyLoss(character, amount));
        Assert.Equal(40, character.DerivedAttributes.Sanity.Value);
        Assert.Equal(0, character.State.LastSanityLoss);
        Assert.Equal(7, character.State.SanityLossEpisode);
    }

    /// <summary>Текущий Рассудок выше максимума (старый лист) сначала прижимается к 99 − Мифы.</summary>
    [Fact]
    [Trait("page", "153")]
    public void ApplyLoss_CurrentAboveMax_ClampsBeforeLoss()
    {
        var character = WithMythos(20, sanity: 90);

        Assert.Equal(5, SanityRules.ApplyLoss(character, 5));
        Assert.Equal(74, character.DerivedAttributes.Sanity.Value);
    }

    [Theory]
    [Trait("page", "156")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ApplyLoss_AlreadyInsane_AnyLossMakesBoutDue(bool temporary, bool indefinite)
    {
        var character = Character(sanity: 40);
        character.State.HasTemporaryInsanity = temporary;
        character.State.HasIndefiniteInsanity = indefinite;

        SanityRules.ApplyLoss(character, 1);

        Assert.True(character.State.InsanityBoutDue);
    }

    [Fact]
    [Trait("page", "156")]
    public void ApplyLoss_InsaneButNothingLost_BoutNotDue()
    {
        var character = Character(sanity: 40);
        character.State.HasTemporaryInsanity = true;

        SanityRules.ApplyLoss(character, 0);

        Assert.False(character.State.InsanityBoutDue);
    }

    // ── Отметки безумия и приступ ───────────────────────────────────────────

    [Fact]
    [Trait("page", "153-154")]
    public void SetTemporaryInsanity_On_MakesBoutDue_AndStartsClock()
    {
        var character = Character();

        SanityRules.SetTemporaryInsanity(character, true);

        Assert.True(character.State.HasTemporaryInsanity);
        Assert.NotNull(character.State.TemporaryInsanityStartedAt);
        Assert.True(character.State.InsanityBoutDue);
        Assert.True(SanityRules.IsInsane(character));
    }

    [Fact]
    [Trait("page", "154")]
    public void SetIndefiniteInsanity_On_MakesBoutDue_AndStartsClock()
    {
        var character = Character();

        SanityRules.SetIndefiniteInsanity(character, true);

        Assert.True(character.State.HasIndefiniteInsanity);
        Assert.NotNull(character.State.IndefiniteInsanityStartedAt);
        Assert.True(character.State.InsanityBoutDue);
    }

    [Fact]
    [Trait("page", "154")]
    public void ClearingOneMark_WhileOtherRemains_KeepsBoutDue()
    {
        var character = Character();
        SanityRules.SetTemporaryInsanity(character, true);
        SanityRules.SetIndefiniteInsanity(character, true);

        SanityRules.SetTemporaryInsanity(character, false);

        Assert.Null(character.State.TemporaryInsanityStartedAt);
        Assert.True(character.State.InsanityBoutDue);

        SanityRules.SetIndefiniteInsanity(character, false);

        Assert.False(character.State.InsanityBoutDue);
        Assert.False(SanityRules.IsInsane(character));
    }

    [Fact]
    [Trait("page", "155-157")]
    public void RecordBout_StoresResult_AndClearsBoutDue()
    {
        var character = Character();
        SanityRules.SetTemporaryInsanity(character, true);

        SanityRules.RecordBout(character, InsanityBoutMode.Summary, 7, 4);

        Assert.False(character.State.InsanityBoutDue);
        Assert.NotNull(character.State.LastInsanityBout);
        Assert.Equal(InsanityBoutMode.Summary, character.State.LastInsanityBout.Mode);
        Assert.Equal(7, character.State.LastInsanityBout.Roll);
        Assert.Equal(4, character.State.LastInsanityBout.Duration);
        Assert.True(character.State.HasTemporaryInsanity); // приступ безумия не снимает
    }

    [Fact]
    [Trait("page", "154")]
    public void DismissBout_ClearsBoutDue_KeepsMarks()
    {
        var character = Character();
        SanityRules.SetIndefiniteInsanity(character, true);

        SanityRules.DismissBout(character);

        Assert.False(character.State.InsanityBoutDue);
        Assert.True(character.State.HasIndefiniteInsanity);
    }
}
