using Bunit;
using CampaignManager.UI.Shared;
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
}
