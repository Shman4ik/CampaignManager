using System.Text.RegularExpressions;
using Bunit;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Encounters;
using CampaignManager.UI.Encounters;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>UX-1 (U4): тексты стола без номеров страниц, счётчики предпросмотра под «Подробностями», строка участника в одну строку.</summary>
public sealed class EncounterUx4Tests : KitContext
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Выстрел укрылся (стр. 111).", "Выстрел укрылся.")]
    [InlineData("Нужен трудный успех — дальность (стр. 110, 114).", "Нужен трудный успех — дальность.")]
    [InlineData("Починка (Механика или Стрельба, стр. 113)", "Починка (Механика или Стрельба)")]
    [InlineData("Расстановка (стр. 145): Вампир — 1", "Расстановка: Вампир — 1")]
    [InlineData("Без номеров страниц", "Без номеров страниц")]
    public void Table_text_drops_book_pages(string text, string expected) =>
        Assert.Equal(expected, EncounterDisplay.Plain(text));

    [Fact]
    public void Counters_are_not_shown_in_the_preview_at_all()
    {
        var state = new EncounterState();
        var ghoul = EncounterParticipants.FromStatblock(null, "Гуль", new Statblock { HitPoints = 13 });
        EncounterEngine.Add(state, ghoul, Now);
        var resolution = new EncounterResolution
        {
            Title = "Удар",
            Effects =
            [
                new EncounterEffect { Kind = EncounterEffectKind.Damage, ParticipantId = ghoul.Id, Amount = 3 },
                new EncounterEffect { Kind = EncounterEffectKind.Attack, ParticipantId = ghoul.Id, Amount = 1 },
            ],
        };

        var cut = Render<ResolutionPreview>(p => p.Add(c => c.State, state).Add(c => c.Resolution, resolution));

        // Счётчики боя — внутреннее: ни строки в таблице, ни «Подробностей» (B30).
        var main = cut.FindAll("[data-testid=resolution-preview] tbody td").Select(td => td.TextContent.Trim()).ToList();
        Assert.Contains(main, cell => cell.StartsWith("Урон 3", StringComparison.Ordinal));
        Assert.DoesNotContain("за раунд", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("details"));
    }

    [Fact]
    public void Participant_row_is_one_line_with_extras_on_tap()
    {
        var state = new EncounterState();
        var ghoul = EncounterParticipants.FromStatblock(null, "Гуль", new Statblock { HitPoints = 13, Dex = new StatValue { Value = 65 } });
        EncounterEngine.Add(state, ghoul, Now);

        var cut = Render<ParticipantRow>(p => p.Add(c => c.Participant, ghoul));

        Assert.Empty(cut.FindAll("[data-testid=stat-san]"));
        Assert.DoesNotContain("бонус к урону", cut.Markup, StringComparison.Ordinal);
        cut.Find("button.encounter-row-name").Click();
        // Числа — блоками «подпись значение»; вид и сторона в раскрытой строке не повторяются (они — точка и подпись цветов).
        var numbers = cut.Find("[data-testid=participant-numbers]").TextContent;
        Assert.Contains("Бонус к урону", numbers, StringComparison.Ordinal);
        Assert.Contains("Инициатива 65", Regex.Replace(numbers, @"\s+", " "), StringComparison.Ordinal);
        Assert.DoesNotContain("сторона:", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("БкУ", cut.Markup, StringComparison.Ordinal);
    }
}
