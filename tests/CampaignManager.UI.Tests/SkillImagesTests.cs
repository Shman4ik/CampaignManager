using Bunit;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Core.Catalogs;
using CampaignManager.UI.Catalogs;
using CampaignManager.UI.Catalogs.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Иллюстрация навыка — одна, как у книги: миниатюра у названия (без своей — заглушка), в раскрытой строке и в справке листа
/// (тот же <see cref="SkillDetails"/>) — картинка рядом с описанием; специализация без своей показывает картинку родителя.
/// </summary>
public sealed class SkillImagesTests : KitContext
{
    private static readonly Guid FightingImage = Guid.NewGuid();
    private static readonly Guid BrawlImage = Guid.NewGuid();

    private static readonly SkillDto Fighting = new()
    {
        Id = Guid.NewGuid(),
        Code = "skill.fighting",
        Name = "Ближний бой",
        Category = SkillCategory.CombatGeneral,
        ImageFileId = FightingImage,
        ImageUrl = $"/api/v1/files/{FightingImage}",
    };

    private static readonly SkillDto Brawl = new()
    {
        Id = Guid.NewGuid(),
        Code = "skill.fighting.brawl",
        Name = "Ближний бой (драка)",
        ParentId = Fighting.Id,
        Category = SkillCategory.CombatGeneral,
        Description = "Драка простым оружием.",
        ImageFileId = BrawlImage,
        ImageUrl = $"/api/v1/files/{BrawlImage}",
    };

    private static readonly SkillDto Sword = new()
    {
        Id = Guid.NewGuid(),
        Code = "skill.fighting.sword",
        Name = "Ближний бой (меч)",
        ParentId = Fighting.Id,
        Category = SkillCategory.CombatGeneral,
        Description = "Клинок длиннее 60 см.",
    };

    private static readonly SkillDto Accounting = new()
    {
        Id = Guid.NewGuid(),
        Code = "skill.accounting",
        Name = "Бухгалтерское дело",
        Category = SkillCategory.Knowledge,
        Description = "Счета и балансы.",
    };

    private void UseCatalog(bool canEdit = false) =>
        Services.AddSingleton<ICatalogApi<SkillDto>>(new FakeCatalog(canEdit, [Fighting, Brawl, Sword, Accounting]));

    [Fact]
    public void Gallery_groups_tiles_by_category_with_own_cover_or_placeholder()
    {
        UseCatalog();

        var page = Render<SkillsPage>();

        page.WaitForAssertion(() => Assert.Contains("Бухгалтерское дело", page.Markup, StringComparison.Ordinal));
        Assert.Empty(page.FindAll("table"));
        Assert.Equal(2, page.FindAll("section h2").Count); // «Знания» и «Сражение (общее)»
        var covers = page.FindAll(".cm-tile-media img").Select(i => i.GetAttribute("src")).Order().ToList();
        Assert.Equal(new[] { $"/api/v1/files/{FightingImage}?w=480", $"/api/v1/files/{BrawlImage}?w=480" }.Order(), covers);
        Assert.Equal(2, page.FindAll(".cm-tile-media .fa-brain").Count); // меч и бухгалтерия — заглушка того же места
    }

    [Fact]
    public void Table_row_shows_own_thumbnail_and_skill_without_one_gets_placeholder()
    {
        UseCatalog();
        Services.GetRequiredService<NavigationManager>().NavigateTo("skills?view=table");

        var page = Render<SkillsPage>();

        page.WaitForAssertion(() => Assert.Contains("Бухгалтерское дело", page.Markup, StringComparison.Ordinal));
        var thumbs = page.FindAll("img[src*='/api/v1/files/']").Select(i => i.GetAttribute("src")).Distinct().Order().ToList();
        Assert.Equal(new[] { $"/api/v1/files/{FightingImage}?w=240", $"/api/v1/files/{BrawlImage}?w=240" }.Order(), thumbs);
        Assert.NotEmpty(page.FindAll(".cm-thumb .fa-brain"));
    }

    [Fact]
    public void Open_skill_shows_illustration_left_of_details_and_specialization_falls_back_to_parent()
    {
        UseCatalog();
        Services.GetRequiredService<NavigationManager>().NavigateTo($"skills?open={Sword.Id}");

        var page = Render<SkillsPage>();

        // Картинку рисует каркас раскрытой записи слева; своей у SkillDetails в справочнике нет.
        page.WaitForAssertion(() => Assert.NotNull(page.Find(".cm-record figure > img")));
        Assert.Equal($"/api/v1/files/{FightingImage}", page.Find(".cm-record figure > img").GetAttribute("src"));
        Assert.Empty(page.FindAll("[data-testid=skill-image]"));
        Assert.Contains("Клинок длиннее 60 см.", page.Find(".cm-record").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Specialization_without_own_image_shows_parent_one_and_skill_without_any_shows_none()
    {
        var sword = Render<SkillDetails>(p => p.Add(d => d.Skill, Sword).Add(d => d.Catalog, [Fighting, Brawl, Sword, Accounting]));
        var accounting = Render<SkillDetails>(p => p.Add(d => d.Skill, Accounting).Add(d => d.Catalog, [Fighting, Brawl, Sword, Accounting]));

        Assert.Equal($"/api/v1/files/{FightingImage}?w=480", sword.Find("[data-testid=skill-image]").GetAttribute("src"));
        Assert.Contains("Клинок длиннее 60 см.", sword.Markup, StringComparison.Ordinal);
        Assert.Empty(accounting.FindAll("img"));
    }

    // Загрузка пачкой: у справочника навыков теперь есть обложка — Хранитель видит «Загрузить картинки» в «⋯».
    [Fact]
    public void Keeper_menu_offers_bulk_image_upload()
    {
        UseCatalog(canEdit: true);

        var page = Render<SkillsPage>();

        page.WaitForAssertion(() => Assert.Contains("картинки", page.Find("button[aria-label^='Ещё:']").GetAttribute("aria-label"), StringComparison.Ordinal));
    }

    private sealed class FakeCatalog(bool canEdit, IReadOnlyList<SkillDto> items) : ICatalogApi<SkillDto>
    {
        public CatalogRoute Route => CatalogsRoutes.Skills;

        public Task<CatalogList<SkillDto>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new CatalogList<SkillDto>(items, canEdit));

        public Task<SkillDto> CreateAsync(SkillDto item, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SkillDto> UpdateAsync(SkillDto item, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CatalogImportReport> ImportAsync(Stream file, bool overwrite, bool dryRun, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CatalogImportReport> SyncAsync(bool dryRun, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
