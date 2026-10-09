using Bunit;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Core.Catalogs;
using CampaignManager.UI.Catalogs.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Справочник артефактов (гл. 13) — карточки: вид, владельцы и «В игре» на карточке, фильтр по владельцам из самих записей,
/// порядок выбором, оружие-артефакт ведёт к записи справочника оружия с тем же названием; слота обложки нет, пока картинок
/// нет ни у кого, а с первой картинкой у остальных — заглушка.
/// </summary>
public sealed class ArtifactsPageTests : KitContext
{
    private static readonly Guid GunWeapon = Guid.NewGuid();

    private readonly ArtifactDto _gun = new()
    {
        Id = Guid.NewGuid(),
        Name = "Молниемёт",
        Kind = ArtifactKind.Weapon,
        UsedBy = ["йитиане"],
        Rule = "Стрельба (молниемёт) 10%",
        Description = "Стреляет разрядами.",
    };

    private readonly ArtifactDto _armor = new()
    {
        Id = Guid.NewGuid(),
        Name = "Биопаутинная броня",
        Kind = ArtifactKind.Armor,
        UsedBy = ["ми-го"],
        Rule = "Броня 8",
    };

    public ArtifactsPageTests()
    {
        Services.AddSingleton<ICatalogApi<ArtifactDto>>(new FakeCatalog<ArtifactDto>(CatalogsRoutes.Artifacts, [_gun, _armor]));
        Services.AddSingleton<ICatalogApi<WeaponDto>>(new FakeCatalog<WeaponDto>(CatalogsRoutes.Weapons,
        [
            new WeaponDto { Id = GunWeapon, Name = "молниемет", Damage = "1d10", SkillName = "Стрельба (молниемёт)", Type = WeaponType.Other },
        ]));
    }

    [Fact]
    public void Cards_show_kind_owners_and_rule_without_cover_slot()
    {
        var page = Render<ArtifactsPage>();

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".cm-wide").Count));
        var armor = page.FindAll(".cm-wide").Single(c => c.TextContent.Contains("Биопаутинная броня", StringComparison.Ordinal));
        Assert.Contains("Броня · Ми-го", armor.TextContent, StringComparison.Ordinal);
        Assert.Contains("Броня 8", armor.TextContent, StringComparison.Ordinal);
        Assert.Empty(page.FindAll(".cm-wide-cover"));
    }

    [Fact]
    public void First_image_adds_covers_and_placeholders_for_the_rest()
    {
        var file = Guid.NewGuid();
        _armor.Images = [new CatalogImageDto(file, $"/api/v1/files/{file}", null)];

        var page = Render<ArtifactsPage>();

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".cm-wide-cover").Count));
        Assert.Equal($"/api/v1/files/{file}?w=240", page.Find(".cm-wide-cover img").GetAttribute("src"));
        var gun = page.FindAll(".cm-wide").Single(c => c.TextContent.Contains("Молниемёт", StringComparison.Ordinal));
        Assert.NotNull(gun.QuerySelector(".cm-wide-cover .fa-gem"));
    }

    [Fact]
    public void Sort_select_orders_cards_by_owner()
    {
        var page = Render<ArtifactsPage>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".cm-wide").Count));
        Assert.StartsWith("Биопаутинная броня", page.FindAll(".cm-wide")[0].TextContent.Trim(), StringComparison.Ordinal);

        page.FindAll("select").Single(s => s.GetAttribute("aria-label") == "Сортировка").Change("owner");

        // «Йитиане» раньше «Ми-го».
        page.WaitForAssertion(() => Assert.StartsWith("Молниемёт", page.FindAll(".cm-wide")[0].TextContent.Trim(), StringComparison.Ordinal));
    }

    [Fact]
    public void Owner_filter_lists_owners_from_records()
    {
        var page = Render<ArtifactsPage>();
        page.WaitForAssertion(() => Assert.Contains("Молниемёт", page.Markup, StringComparison.Ordinal));

        var owners = page.FindAll("select").Single(s => s.TextContent.Contains("Любые владельцы", StringComparison.Ordinal));
        Assert.Equal(["Любые владельцы", "Йитиане", "Ми-го"], owners.QuerySelectorAll("option").Select(o => o.TextContent.Trim()));

        owners.Change("ми-го");

        page.WaitForAssertion(() => Assert.DoesNotContain("Молниемёт", page.Markup, StringComparison.Ordinal));
        Assert.Contains("Биопаутинная броня", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Open_weapon_artifact_links_to_weapon_for_combat()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo($"artifacts?open={_gun.Id}");

        var page = Render<ArtifactsPage>();

        page.WaitForAssertion(() => Assert.NotNull(page.Find($"a[href='weapons?open={GunWeapon}']")));
        var open = page.Find(".cm-record");
        Assert.Contains("В игре: Стрельба (молниемёт) 10%", open.TextContent, StringComparison.Ordinal);
        Assert.Contains("Подобрать оружие", open.TextContent, StringComparison.Ordinal);
        Assert.Contains("Стреляет разрядами.", open.TextContent, StringComparison.Ordinal);
        Assert.Single(page.FindAll(".cm-wide"));
    }

    private sealed class FakeCatalog<T>(CatalogRoute route, IReadOnlyList<T> items) : ICatalogApi<T>
        where T : CatalogItemDto
    {
        public CatalogRoute Route => route;

        public Task<CatalogList<T>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new CatalogList<T>(items, false));

        public Task<T> CreateAsync(T item, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<T> UpdateAsync(T item, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CatalogImportReport> ImportAsync(Stream file, bool overwrite, bool dryRun, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CatalogImportReport> SyncAsync(bool dryRun, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
