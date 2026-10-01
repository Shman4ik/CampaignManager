using CampaignManager.Core.Characters;

namespace CampaignManager.Core.Tests.Sheet;

/// <summary>Целостность таблиц главы 8: номера строк без пропусков и дублей, тексты не пустые.</summary>
public sealed class InsanityTablesTests
{
    [Theory]
    [Trait("page", "155, 157")]
    [InlineData(InsanityBoutMode.RealTime)]
    [InlineData(InsanityBoutMode.Summary)]
    public void BoutTable_Rows1To10_NoGapsNoDuplicates_TextsFilled(InsanityBoutMode mode)
    {
        var rows = InsanityTables.Bouts(mode);

        Assert.Equal(Enumerable.Range(1, InsanityTables.BoutDie), rows.Select(r => r.Number));
        Assert.All(rows, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Title));
            Assert.False(string.IsNullOrWhiteSpace(r.ReadAloud));
            Assert.False(string.IsNullOrWhiteSpace(r.KeeperNote));
        });
        Assert.Equal(rows.Count, rows.Select(r => r.Title).Distinct().Count());
    }

    [Theory]
    [Trait("page", "155, 157")]
    [InlineData(InsanityBoutMode.RealTime)]
    [InlineData(InsanityBoutMode.Summary)]
    public void BoutTable_Rows9And10_LeadToPhobiaAndMania(InsanityBoutMode mode)
    {
        var rows = InsanityTables.Bouts(mode);

        Assert.Equal(InsanityConditionKind.Phobia, rows[8].Acquires);
        Assert.Equal(InsanityConditionKind.Mania, rows[9].Acquires);
        Assert.All(rows.Take(8), r => Assert.Null(r.Acquires));
    }

    [Theory]
    [Trait("page", "158-159")]
    [InlineData(InsanityConditionKind.Phobia)]
    [InlineData(InsanityConditionKind.Mania)]
    public void ConditionTable_Rows1To100_NoGapsNoOverlaps_TextsFilled(InsanityConditionKind kind)
    {
        var rows = InsanityTables.Conditions(kind);

        Assert.Equal(Enumerable.Range(1, InsanityTables.ConditionDie), rows.Select(r => r.Number));
        Assert.All(rows, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Name));
            Assert.False(string.IsNullOrWhiteSpace(r.Description));
        });
        Assert.Equal(rows.Count, rows.Select(r => r.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    [Trait("page", "158-159")]
    public void Tables_PhobiasAndManiasAreDifferentLists()
    {
        Assert.Same(InsanityTables.Phobias, InsanityTables.Conditions(InsanityConditionKind.Phobia));
        Assert.Same(InsanityTables.Manias, InsanityTables.Conditions(InsanityConditionKind.Mania));
        Assert.Empty(InsanityTables.Phobias.Select(p => p.Name).Intersect(InsanityTables.Manias.Select(m => m.Name)));
    }

    [Theory]
    [Trait("page", "155, 157")]
    [InlineData(InsanityBoutMode.RealTime)]
    [InlineData(InsanityBoutMode.Summary)]
    public void FindBout_EveryRollFindsItsRow_OutsideIsNull(InsanityBoutMode mode)
    {
        for (var roll = 1; roll <= InsanityTables.BoutDie; roll++)
            Assert.Equal(roll, InsanityTables.FindBout(mode, roll)?.Number);

        Assert.Null(InsanityTables.FindBout(mode, 0));
        Assert.Null(InsanityTables.FindBout(mode, 11));
    }

    [Theory]
    [Trait("page", "158-159")]
    [InlineData(InsanityConditionKind.Phobia)]
    [InlineData(InsanityConditionKind.Mania)]
    public void FindCondition_EveryRollFindsItsRow_OutsideIsNull(InsanityConditionKind kind)
    {
        for (var roll = 1; roll <= InsanityTables.ConditionDie; roll++)
            Assert.Equal(roll, InsanityTables.FindCondition(kind, roll)?.Number);

        Assert.Null(InsanityTables.FindCondition(kind, 0));
        Assert.Null(InsanityTables.FindCondition(kind, 101));
    }

    [Fact]
    [Trait("page", "155-159")]
    public void TableNumeralsAndPages()
    {
        Assert.Equal(("VII", 155), (InsanityTables.TableNumeral(InsanityBoutMode.RealTime), InsanityTables.TablePage(InsanityBoutMode.RealTime)));
        Assert.Equal(("VIII", 157), (InsanityTables.TableNumeral(InsanityBoutMode.Summary), InsanityTables.TablePage(InsanityBoutMode.Summary)));
        Assert.Equal(("IX", 158), (InsanityTables.TableNumeral(InsanityConditionKind.Phobia), InsanityTables.TablePage(InsanityConditionKind.Phobia)));
        Assert.Equal(("X", 159), (InsanityTables.TableNumeral(InsanityConditionKind.Mania), InsanityTables.TablePage(InsanityConditionKind.Mania)));
    }

    [Theory]
    [Trait("page", "155-156")]
    [InlineData(InsanityBoutMode.Summary, 1, "1 час")]
    [InlineData(InsanityBoutMode.Summary, 2, "2 часа")]
    [InlineData(InsanityBoutMode.Summary, 5, "5 часов")]
    [InlineData(InsanityBoutMode.Summary, 11, "11 часов")]
    [InlineData(InsanityBoutMode.Summary, 21, "21 час")]
    [InlineData(InsanityBoutMode.RealTime, 1, "1 раунд")]
    [InlineData(InsanityBoutMode.RealTime, 4, "4 раунда")]
    [InlineData(InsanityBoutMode.RealTime, 10, "10 раундов")]
    [InlineData(InsanityBoutMode.RealTime, 12, "12 раундов")]
    public void DurationText_RussianPlurals(InsanityBoutMode mode, int amount, string expected) =>
        Assert.Equal(expected, InsanityTables.DurationText(mode, amount));

    [Fact]
    [Trait("page", "155-156")]
    public void DurationUnit_GenitivePlural()
    {
        Assert.Equal("раундов", InsanityTables.DurationUnit(InsanityBoutMode.RealTime));
        Assert.Equal("часов", InsanityTables.DurationUnit(InsanityBoutMode.Summary));
        Assert.Equal(10, InsanityTables.BoutDurationDie);
    }
}
