using Xunit;

namespace CampaignManager.Core.Tests;

/// <summary>Возраст в карточках и итоге помощника: «1 год», «22 года», «42 года», «11 лет» (UX-повтор R2).</summary>
public sealed class TermsYearsTests
{
    [Theory]
    [InlineData(1, "1 год")]
    [InlineData(2, "2 года")]
    [InlineData(5, "5 лет")]
    [InlineData(11, "11 лет")]
    [InlineData(14, "14 лет")]
    [InlineData(21, "21 год")]
    [InlineData(22, "22 года")]
    [InlineData(34, "34 года")]
    [InlineData(42, "42 года")]
    [InlineData(64, "64 года")]
    [InlineData(100, "100 лет")]
    [InlineData(111, "111 лет")]
    public void Age_is_declined(int age, string expected) => Assert.Equal(expected, Terms.Years(age));
}
