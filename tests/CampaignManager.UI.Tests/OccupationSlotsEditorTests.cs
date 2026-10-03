using Bunit;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Core.Catalogs;
using CampaignManager.UI.Catalogs;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>Слоты профессии: порядок меняют «выше»/«ниже», крайние слоты без лишней стрелки.</summary>
public sealed class OccupationSlotsEditorTests : KitContext
{
    [Fact]
    public void Moving_a_slot_swaps_it_with_its_neighbour()
    {
        List<OccupationSlotDto>? saved = null;
        var first = new OccupationSlotDto { Kind = OccupationSlotKind.Social };
        var second = new OccupationSlotDto { Kind = OccupationSlotKind.Free };

        var editor = Render<OccupationSlotsEditor>(p => p
            .Add(c => c.Slots, [first, second])
            .Add(c => c.SlotsChanged, list => saved = list));

        Assert.Empty(editor.FindAll("[aria-label='Слот 1 выше']"));
        Assert.Empty(editor.FindAll("[aria-label='Слот 2 ниже']"));
        editor.Find("[aria-label='Слот 1 ниже']").Click();

        Assert.Equal([OccupationSlotKind.Free, OccupationSlotKind.Social], saved!.Select(s => s.Kind));
    }
}
