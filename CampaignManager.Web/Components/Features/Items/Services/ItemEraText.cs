using CampaignManager.Web.Components.Shared.Model;

namespace CampaignManager.Web.Components.Features.Items.Services;

/// <summary>
///     Эпоха предмета одной строкой по-русски. <see cref="Eras" /> — флаги: предмет
///     встречается и в 1920-х, и в наши дни одновременно, поэтому это перечисление
///     значений, а не выбор одного из трёх.
/// </summary>
public static class ItemEraText
{
    public static string Describe(Eras era)
    {
        List<string> parts = [];
        if ((era & Eras.Classic) != 0) parts.Add("1920-е");
        if ((era & Eras.Modern) != 0) parts.Add("наши дни");

        return parts.Count == 0 ? "—" : string.Join(", ", parts);
    }
}
