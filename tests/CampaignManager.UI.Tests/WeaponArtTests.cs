using Bunit;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Core.Catalogs;
using CampaignManager.UI.Catalogs;
using CampaignManager.UI.Catalogs.Pages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Иллюстрации оружия: список кодов (<c>WeaponArt.Codes.cs</c>) и файлы в <c>wwwroot/img/weapons</c> пишет один скрипт,
/// но правка руками может их развести — тогда строка таблицы покажет битую картинку. Картинка — только у книжной записи.
/// </summary>
public sealed class WeaponArtTests : KitContext
{
    private static readonly string ArtDirectory = Path.Combine(RepoRoot(), "src", "CampaignManager.UI", "wwwroot", "img", "weapons");

    [Fact]
    public void Every_listed_code_is_a_book_weapon_with_both_files()
    {
        Assert.NotEmpty(WeaponArt.All);
        foreach (var code in WeaponArt.All)
        {
            Assert.True(WeaponCodes.Table.BookNames.ContainsKey(code), $"{code} нет в WeaponCodes");
            var slug = code[WeaponCodes.Prefix.Length..];
            Assert.True(File.Exists(Path.Combine(ArtDirectory, $"{slug}.webp")), $"{slug}.webp");
            Assert.True(File.Exists(Path.Combine(ArtDirectory, $"{slug}.thumb.webp")), $"{slug}.thumb.webp");
        }
    }

    [Fact]
    public void Every_file_belongs_to_a_listed_code()
    {
        var listed = WeaponArt.All.Select(c => c[WeaponCodes.Prefix.Length..]).ToHashSet(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(ArtDirectory))
        {
            var slug = Path.GetFileName(file).Replace(".thumb.webp", "", StringComparison.Ordinal).Replace(".webp", "", StringComparison.Ordinal);
            Assert.True(listed.Contains(slug), $"{Path.GetFileName(file)} не в списке WeaponArt");
        }
    }

    [Fact]
    public void Book_weapon_gets_a_thumbnail_and_homemade_gets_an_empty_slot()
    {
        Services.AddSingleton<ICatalogApi<WeaponDto>>(new FakeCatalog<WeaponDto>(CatalogsRoutes.Weapons,
        [
            new WeaponDto { Id = Guid.NewGuid(), Code = "weapon.thompson", Name = "«Томпсон»", Damage = "1d10" },
            new WeaponDto { Id = Guid.NewGuid(), Name = "Самопал", Damage = "1d6" },
        ]));
        Services.AddSingleton<ICatalogApi<SkillDto>>(new FakeCatalog<SkillDto>(CatalogsRoutes.Skills, []));

        var page = Render<WeaponsPage>();

        page.WaitForAssertion(() => Assert.Contains("Самопал", page.Markup, StringComparison.Ordinal));
        var images = page.FindAll("img[src*='img/weapons/']");
        Assert.NotEmpty(images);
        Assert.All(images, img => Assert.EndsWith("/thompson.thumb.webp", img.GetAttribute("src"), StringComparison.Ordinal));
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CampaignManager.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Не найден корень репозитория (CampaignManager.slnx).");
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
