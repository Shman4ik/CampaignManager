using CampaignManager.ApiClient.Catalogs;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Identity;
using CampaignManager.Data;
using CampaignManager.Data.Catalogs;
using CampaignManager.Data.Identity;
using CampaignManager.Server.Tests.Schema;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CampaignManager.Server.Tests.Catalogs;

/// <summary>
/// Сервер со своей базой (<see cref="SchemaDatabase"/>) и тремя людьми: Хранитель, игрок, админ.
/// Навыки книги заведены заранее (все коды <see cref="SkillCodes"/> с родителями) — на них ссылаются
/// оружие и профессии, и по ним синхронизация с правилами находит навыки сида.
/// </summary>
public sealed class CatalogsApp : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;

    public SchemaDatabase Database { get; } = new();

    public User Keeper { get; } = new() { Email = $"keeper-{Guid.NewGuid():N}@example.test", DisplayName = "Хранитель", Role = UserRole.Keeper };

    public User Player { get; } = new() { Email = $"player-{Guid.NewGuid():N}@example.test", DisplayName = "Игрок" };

    public User Admin { get; } = new() { Email = $"admin-{Guid.NewGuid():N}@example.test", DisplayName = "Админ", Role = UserRole.Admin };

    /// <summary>Навыки книги: код → id.</summary>
    public Dictionary<string, Guid> Skills { get; } = [];

    public async ValueTask InitializeAsync()
    {
        await Database.InitializeAsync();
        if (TestDatabase.ConnectionString is null)
        {
            return;
        }

        await using (var db = Database.CreateContext())
        {
            db.Users.AddRange(Keeper, Player, Admin);
            // Родители раньше специализаций: код специализации длиннее на сегмент.
            foreach (var (code, name) in SkillCodes.BookNames.OrderBy(p => p.Key.Count(c => c == '.')))
            {
                var parent = SkillCodes.ParentOf(code) is { } parentCode && Skills.TryGetValue(parentCode, out var parentId)
                    ? parentId
                    : (Guid?)null;
                var skill = new Skill { Code = code, Name = name, ParentId = parent, BaseValue = 5, Category = SkillCategory.Knowledge };
                Skills[code] = skill.Id;
                db.Skills.Add(skill);
            }

            await db.SaveChangesAsync();
        }

        _factory = new CmApp().WithWebHostBuilder(builder =>
            builder.UseSetting($"ConnectionStrings:{CmDatabase.ConnectionStringName}", Database.ConnectionString));
    }

    public HttpClient CreateClient(User? user = null, bool anonymous = false)
    {
        var client = (_factory ?? throw new InvalidOperationException(
            "Сервер не поднят: тест должен начинаться с TestDatabase.SkipIfMissing().")).CreateClient();
        return anonymous ? client : client.As((user ?? Keeper).Id);
    }

    public WeaponsApiClient Weapons(User? user = null) => new(CreateClient(user));

    public SkillsApiClient SkillsApi(User? user = null) => new(CreateClient(user));

    public OccupationsApiClient Occupations(User? user = null) => new(CreateClient(user));

    public BooksApiClient Books(User? user = null) => new(CreateClient(user));

    public SpellsApiClient Spells(User? user = null) => new(CreateClient(user));

    public CreaturesApiClient Creatures(User? user = null) => new(CreateClient(user));

    public ItemsApiClient Items(User? user = null) => new(CreateClient(user));

    public ArtifactsApiClient Artifacts(User? user = null) => new(CreateClient(user));

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await Database.DisposeAsync();
    }
}
