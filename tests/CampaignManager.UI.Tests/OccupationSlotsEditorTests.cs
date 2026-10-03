using Bunit;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Core.Catalogs;
using CampaignManager.UI.Catalogs;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>Слоты профессии: порядок меняют «Выше»/«Ниже» в меню строки «⋯», крайним слотам лишнего пункта нет (O5).</summary>
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

        editor.Find("button[aria-label='Действия: слот 1']").Click();
        Assert.DoesNotContain(editor.FindAll("[role=menuitem]"), i => i.TextContent.Contains("Выше", StringComparison.Ordinal));
        editor.FindAll("[role=menuitem]").Single(i => i.TextContent.Contains("Ниже", StringComparison.Ordinal)).Click();

        Assert.Equal([OccupationSlotKind.Free, OccupationSlotKind.Social], saved!.Select(s => s.Kind));
    }
}
