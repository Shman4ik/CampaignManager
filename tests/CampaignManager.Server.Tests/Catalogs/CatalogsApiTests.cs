using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Files;
using CampaignManager.Contracts.Identity;
using CampaignManager.Contracts.Platform;
using CampaignManager.Core;
using CampaignManager.Core.Admin;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Data.Characters;
using CampaignManager.Data.Files;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CampaignManager.Server.Tests.Catalogs;

/// <summary>
/// Справочники через ApiClient (T2.1): права (игроку — чтение и 403 на любую запись), дубль имени и
/// устаревшая версия — 409 с кодом, ETag списка, разбор оружия при записи, удаление занятой записи,
/// импорт и синхронизация профессий с правилами, журнал правок.
/// </summary>
public sealed class CatalogsApiTests(CatalogsApp app) : IClassFixture<CatalogsApp>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static string Unique(string name) => $"{name} {Guid.NewGuid():N}"[..(name.Length + 9)];

    private WeaponDto Weapon(string name, string damage = "1d10+2") => new()
    {
        Name = name,
        Type = WeaponType.Pistols,
        SkillId = app.Skills["skill.firearms.handgun"],
        Damage = damage,
        Range = "15 м",
        Attacks = "1 (3)",
        Ammo = "7",
        Malfunction = 100,
        Cost = "$40/$500",
    };

    // ── Права ──

    [Fact]
    public async Task Player_reads_catalog_without_edit_rights()
    {
        TestDatabase.SkipIfMissing();

        var asPlayer = await app.SkillsApi(app.Player).ListAsync(Cancellation);
        var asKeeper = await app.SkillsApi(app.Keeper).ListAsync(Cancellation);

        Assert.False(asPlayer.CanEdit);
        Assert.True(asKeeper.CanEdit);
        Assert.Equal(asKeeper.Items.Count, asPlayer.Items.Count);
    }

    [Fact]
    public async Task Player_write_requests_are_forbidden()
    {
        TestDatabase.SkipIfMissing();
        var existing = await app.Weapons().CreateAsync(Weapon(Unique("Кольт")), Cancellation);
        var player = app.Weapons(app.Player);

        var create = await Assert.ThrowsAsync<ApiException>(() => player.CreateAsync(Weapon(Unique("Дерринджер")), Cancellation));
        var update = await Assert.ThrowsAsync<ApiException>(() => player.UpdateAsync(existing, Cancellation));
        var delete = await Assert.ThrowsAsync<ApiException>(() => player.DeleteAsync(existing.Id, Cancellation));
        var import = await Assert.ThrowsAsync<ApiException>(() =>
            player.ImportAsync(Json("""{"catalog":"weapons","items":[]}"""), overwrite: true, dryRun: false, Cancellation));
        var sync = await Assert.ThrowsAsync<ApiException>(() => app.Occupations(app.Player).SyncAsync(dryRun: true, Cancellation));

        Assert.All([create, update, delete, import, sync], ex => Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode));
        Assert.NotNull(await app.Weapons().ListAsync(Cancellation) is { } list ? list.Items.SingleOrDefault(w => w.Id == existing.Id) : null);
    }

    [Fact]
    public async Task Catalog_is_closed_to_anonymous()
    {
        TestDatabase.SkipIfMissing();

        using var response = await app.CreateClient(anonymous: true).GetAsync(CatalogsRoutes.Weapons.Base, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_edits_like_keeper()
    {
        TestDatabase.SkipIfMissing();

        var created = await app.Weapons(app.Admin).CreateAsync(Weapon(Unique("Маузер")), Cancellation);

        Assert.NotEqual(Guid.Empty, created.Id);
    }

    // ── Запись ──

    [Fact]
    public async Task Duplicate_name_is_conflict_with_clear_text()
    {
        TestDatabase.SkipIfMissing();
        var name = Unique("Винчестер");
        await app.Weapons().CreateAsync(Weapon(name), Cancellation);

        var duplicate = await Assert.ThrowsAsync<ApiException>(() =>
            app.Weapons().CreateAsync(Weapon("  " + name.ToUpperInvariant() + " "), Cancellation));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(ApiProblemCodes.Duplicate, duplicate.Code);
        Assert.Contains("уже есть", duplicate.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stale_version_is_conflict_not_silent_overwrite()
    {
        TestDatabase.SkipIfMissing();
        var created = await app.Weapons().CreateAsync(Weapon(Unique("Браунинг")), Cancellation);
        var first = Copy(created);
        first.Notes = "правка с iPad";
        var saved = await app.Weapons().UpdateAsync(first, Cancellation);

        var second = Copy(created);
        second.Notes = "правка с телефона";
        var stale = await Assert.ThrowsAsync<ApiException>(() => app.Weapons().UpdateAsync(second, Cancellation));

        Assert.NotEqual(created.Version, saved.Version);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.True(stale.IsStale);
        var list = await app.Weapons().ListAsync(Cancellation);
        Assert.Equal("правка с iPad", list.Items.Single(w => w.Id == created.Id).Notes);
    }

    [Fact]
    public async Task Update_without_version_is_precondition_required()
    {
        TestDatabase.SkipIfMissing();
        var created = await app.Weapons().CreateAsync(Weapon(Unique("Люгер")), Cancellation);

        using var response = await app.CreateClient().PutAsync(CatalogsRoutes.Weapons.Item(created.Id),
            new StringContent("""{"name":"Люгер Р08"}""", Encoding.UTF8, "application/json"), Cancellation);

        Assert.Equal(HttpStatusCode.PreconditionRequired, response.StatusCode);
    }

    [Fact]
    public async Task List_answers_not_modified_by_etag()
    {
        TestDatabase.SkipIfMissing();
        var client = app.CreateClient(app.Player);

        using var first = await client.GetAsync(CatalogsRoutes.Skills.Base, Cancellation);
        var etag = first.Headers.ETag!;
        using var request = new HttpRequestMessage(HttpMethod.Get, CatalogsRoutes.Skills.Base);
        request.Headers.IfNoneMatch.Add(etag);
        using var second = await client.SendAsync(request, Cancellation);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Contains("no-cache", first.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task Weapon_numbers_are_parsed_once_on_write()
    {
        TestDatabase.SkipIfMissing();

        var shotgun = await app.Weapons().CreateAsync(Weapon(Unique("Дробовик"), "4d6/2d6/1d6"), Cancellation);
        var pistol = await app.Weapons().CreateAsync(Weapon(Unique("Кольт 45")), Cancellation);

        Assert.Equal(3, shotgun.DamageByRange!.Count);
        Assert.Equal("4d6", shotgun.DamageByRange[0].Damage);
        Assert.Equal(15, pistol.BaseRangeM);
        Assert.Equal(1, pistol.ShotsPerRound);
        Assert.Equal(3, pistol.MaxShotsPerRound);
        Assert.Equal(7, pistol.AmmoCapacity);
        Assert.Equal(40m, pistol.CostClassic);
        Assert.Equal(500m, pistol.CostModern);
        Assert.Equal("Стрельба (пистолет)", pistol.SkillName);
    }

    [Fact]
    public async Task Weapon_images_keep_order_and_captions_like_bestiary()
    {
        TestDatabase.SkipIfMissing();
        Guid side, drum;
        await using (var db = app.Database.CreateContext())
        {
            var files = new[] { new StoredFile { ExternalUrl = "https://example.test/side.webp" }, new StoredFile { ExternalUrl = "https://example.test/drum.webp" } };
            db.Files.AddRange(files);
            await db.SaveChangesAsync(Cancellation);
            (side, drum) = (files[0].Id, files[1].Id);
        }

        var weapon = Weapon(Unique("Томпсон"));
        weapon.Images = [new CatalogImageDto(side, null, " Вид сбоку "), new CatalogImageDto(drum, null, null)];
        var created = await app.Weapons().CreateAsync(weapon, Cancellation);

        Assert.Equal([side, drum], created.Images.Select(i => i.FileId));
        Assert.Equal("Вид сбоку", created.Images[0].Caption);
        Assert.Equal(FilesRoutes.Content(side), created.Images[0].Url);

        // Обложкой стал барабан, вид сбоку убран — правка только детей тоже сдвигает версию записи.
        created.Images = [created.Images[1]];
        var updated = await app.Weapons().UpdateAsync(created, Cancellation);
        Assert.Equal([drum], updated.Images.Select(i => i.FileId));
        Assert.NotEqual(created.Version, updated.Version);

        updated.Images = [new CatalogImageDto(Guid.NewGuid(), null, null)];
        var missing = await Assert.ThrowsAsync<ApiException>(() => app.Weapons().UpdateAsync(updated, Cancellation));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    private async Task<Guid[]> FilesAsync(int count)
    {
        await using var db = app.Database.CreateContext();
        var files = Enumerable.Range(0, count).Select(_ => new StoredFile { ExternalUrl = $"https://example.test/{Guid.NewGuid():N}.webp" }).ToArray();
        db.Files.AddRange(files);
        await db.SaveChangesAsync(Cancellation);
        return [.. files.Select(f => f.Id)];
    }

    // Картинки пачкой: обложка ставится одним PUT …/cover, без формы и If-Match; есть — только с «заменить».
    [Fact]
    public async Task Item_cover_is_set_once_and_replaced_only_on_request()
    {
        TestDatabase.SkipIfMissing();
        var files = await FilesAsync(2);
        var item = await app.Items().CreateAsync(new ItemDto { Name = Unique("Замок") }, Cancellation);

        var covered = await app.Items().SetCoverAsync(item.Id, files[0], replace: false, Cancellation);
        Assert.Equal(files[0], covered.ImageFileId);
        Assert.Equal(FilesRoutes.Content(files[0]), covered.ImageUrl);
        Assert.NotEqual(item.Version, covered.Version); // открытая на другом устройстве форма получит 409, а не затрёт обложку

        var taken = await Assert.ThrowsAsync<ApiException>(() => app.Items().SetCoverAsync(item.Id, files[1], replace: false, Cancellation));
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal(ApiProblemCodes.Conflict, taken.Code);

        var same = await app.Items().SetCoverAsync(item.Id, files[0], replace: false, Cancellation); // та же картинка — не отказ
        Assert.Equal(covered.Version, same.Version);

        var replaced = await app.Items().SetCoverAsync(item.Id, files[1], replace: true, Cancellation);
        Assert.Equal(files[1], replaced.ImageFileId);

        var missing = await Assert.ThrowsAsync<ApiException>(() => app.Items().SetCoverAsync(item.Id, Guid.NewGuid(), replace: true, Cancellation));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var player = await Assert.ThrowsAsync<ApiException>(() => app.Items(app.Player).SetCoverAsync(item.Id, files[0], replace: true, Cancellation));
        Assert.Equal(HttpStatusCode.Forbidden, player.StatusCode);
    }

    // Иллюстрация навыка — одна, как у предмета: поле формы, обложка пачкой; картинка из файла другой базы не найдётся —
    // запись без неё и предупреждение, а перезапись своей картинкой называет поле в отчёте.
    [Fact]
    public async Task Skill_has_one_image_like_item()
    {
        TestDatabase.SkipIfMissing();
        var files = await FilesAsync(2);
        var skills = app.SkillsApi();
        var skill = await skills.CreateAsync(new SkillDto { Name = Unique("Гипноз"), Category = SkillCategory.Social, ImageFileId = files[0] }, Cancellation);
        Assert.Equal(files[0], skill.ImageFileId);
        Assert.Equal(FilesRoutes.Content(files[0]), skill.ImageUrl);

        var taken = await Assert.ThrowsAsync<ApiException>(() => skills.SetCoverAsync(skill.Id, files[1], replace: false, Cancellation));
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        var replaced = await skills.SetCoverAsync(skill.Id, files[1], replace: true, Cancellation);
        Assert.Equal(files[1], replaced.ImageFileId);

        replaced.ImageFileId = null;
        Assert.Null((await skills.UpdateAsync(replaced, Cancellation)).ImageUrl);
        var missing = await Assert.ThrowsAsync<ApiException>(() =>
            skills.CreateAsync(new SkillDto { Name = Unique("Чревовещание"), ImageFileId = Guid.NewGuid() }, Cancellation));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var foreign = Unique("Чревовещание");
        var report = await skills.ImportAsync(Json($$"""
            {"catalog":"skills","items":[
              {"name":"{{foreign}}","category":"Social","imageFileId":"{{Guid.NewGuid()}}"},
              {"name":"{{skill.Name}}","category":"Social","imageFileId":"{{files[0]}}"}]}
            """), overwrite: true, dryRun: false, Cancellation);
        Assert.Equal((1, 1), (report.Created, report.Updated));
        Assert.Contains(report.Warnings, w => w.StartsWith($"{foreign}: картинки из файла нет", StringComparison.Ordinal));
        Assert.Contains("изменено: картинка", report.Lines.Single(l => l.Name == skill.Name).Message, StringComparison.Ordinal);
        var list = (await skills.ListAsync(Cancellation)).Items;
        Assert.Null(list.Single(s => s.Name == foreign).ImageFileId);
        Assert.Equal(files[0], list.Single(s => s.Id == skill.Id).ImageFileId);
    }

    [Fact]
    public async Task Cover_of_image_list_replaces_the_first_and_keeps_the_rest()
    {
        TestDatabase.SkipIfMissing();
        var files = await FilesAsync(3);
        var (side, drum, fresh) = (files[0], files[1], files[2]);
        var bare = await app.Weapons().CreateAsync(Weapon(Unique("Кольт")), Cancellation);
        Assert.Equal([side], (await app.Weapons().SetCoverAsync(bare.Id, side, replace: false, Cancellation)).Images.Select(i => i.FileId));

        var weapon = Weapon(Unique("Томпсон"));
        weapon.Images = [new CatalogImageDto(side, null, "Вид сбоку"), new CatalogImageDto(drum, null, "Барабан")];
        var created = await app.Weapons().CreateAsync(weapon, Cancellation);

        var replaced = await app.Weapons().SetCoverAsync(created.Id, fresh, replace: true, Cancellation);
        Assert.Equal([fresh, drum], replaced.Images.Select(i => i.FileId));
        Assert.Equal([null, "Барабан"], replaced.Images.Select(i => i.Caption)); // подпись ушла с прежней обложкой

        // Обложкой — картинка, что стояла второй: в списке она не повторяется.
        Assert.Equal([drum], (await app.Weapons().SetCoverAsync(created.Id, drum, replace: true, Cancellation)).Images.Select(i => i.FileId));
    }

    // Токен агента со scope catalogs:images находит запись по коду и ставит обложку; поля и справочники без картинок — 403.
    [Fact]
    public async Task Agent_with_catalog_images_scope_sets_covers_and_nothing_else()
    {
        TestDatabase.SkipIfMissing();
        var file = (await FilesAsync(1))[0];
        var item = await app.Items().CreateAsync(new ItemDto { Name = Unique("Фонарь") }, Cancellation);
        var agent = app.CreateClient(anonymous: true).AsMachine("agents", app.Keeper.Email, MachineScopes.CatalogImages);
        var items = new ApiClient.Catalogs.ItemsApiClient(agent);

        Assert.Contains((await items.ListAsync(Cancellation)).Items, i => i.Id == item.Id);
        Assert.Equal(file, (await items.SetCoverAsync(item.Id, file, replace: false, Cancellation)).ImageFileId);

        var edit = await Assert.ThrowsAsync<ApiException>(() => items.UpdateAsync(item, Cancellation));
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.GetAsync(Contracts.Music.MusicRoutes.Tracks.Base, Cancellation)).StatusCode);
        var filesOnly = app.CreateClient(anonymous: true).AsMachine("agents", app.Keeper.Email, MachineScopes.Files);
        Assert.Equal(HttpStatusCode.Forbidden, (await filesOnly.GetAsync(CatalogsRoutes.Items.Base, Cancellation)).StatusCode);
    }

    [Fact]
    public async Task Malfunction_outside_d100_is_rejected()
    {
        TestDatabase.SkipIfMissing();
        var weapon = Weapon(Unique("Самопал"));
        weapon.Malfunction = 101;

        var invalid = await Assert.ThrowsAsync<ApiException>(() => app.Weapons().CreateAsync(weapon, Cancellation));

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(ApiProblemCodes.Invalid, invalid.Code);
    }

    [Fact]
    public async Task Skill_used_by_weapon_cannot_be_deleted()
    {
        TestDatabase.SkipIfMissing();
        var skill = await app.SkillsApi().CreateAsync(new SkillDto { Name = Unique("Стрельба из лука"), Category = SkillCategory.CombatFirearms }, Cancellation);
        var bow = Weapon(Unique("Лук"));
        bow.SkillId = skill.Id;
        await app.Weapons().CreateAsync(bow, Cancellation);

        var inUse = await Assert.ThrowsAsync<ApiException>(() => app.SkillsApi().DeleteAsync(skill.Id, Cancellation));

        Assert.Equal(HttpStatusCode.Conflict, inUse.StatusCode);
        Assert.Equal(ApiProblemCodes.InUse, inUse.Code);
        Assert.Contains($"оружие {bow.Name}", inUse.Message, StringComparison.Ordinal); // отказ называет, что держит навык
    }

    // Подтверждение удаления говорит, кто держит запись, до вопроса, а не отказом после него (X13, W10).
    [Fact]
    public async Task Usage_names_what_blocks_a_skill_and_which_sheets_hold_a_weapon()
    {
        TestDatabase.SkipIfMissing();
        var skill = await app.SkillsApi().CreateAsync(new SkillDto { Name = Unique("Стрельба из пращи"), Category = SkillCategory.CombatFirearms }, Cancellation);
        var sling = Weapon(Unique("Праща"));
        sling.SkillId = skill.Id;
        sling = await app.Weapons().CreateAsync(sling, Cancellation);

        var skillUsage = await app.SkillsApi().UsageAsync(skill.Id, Cancellation);
        Assert.Equal(1, skillUsage.Count);
        Assert.True(skillUsage.Blocks);
        Assert.Contains(sling.Name, skillUsage.Examples[0], StringComparison.Ordinal);
        Assert.Equal(0, (await app.Weapons().UsageAsync(sling.Id, Cancellation)).Count);

        // Оружие лежит в листе ссылкой внутри документа, а не внешним ключом: удалить можно, но Хранитель узнаёт, где оно было.
        await using (var db = app.Database.CreateContext())
        {
            var sheet = JsonDocument.Parse($$"""{"personal":{"name":"Артур Нельсон"},"weapons":[{"catalogWeaponId":"{{sling.Id}}"}]}""");
            db.Characters.Add(new Character { Kind = CharacterKind.Npc, Status = CharacterStatus.Active, Sheet = sheet, SheetVersion = 1 });
            await db.SaveChangesAsync(Cancellation);
        }

        var weaponUsage = await app.Weapons().UsageAsync(sling.Id, Cancellation);
        Assert.Equal(1, weaponUsage.Count);
        Assert.False(weaponUsage.Blocks);
        Assert.Equal("лист сыщика Артур Нельсон", weaponUsage.Examples[0]);
        Assert.NotNull(weaponUsage.Note);
        await app.Weapons().DeleteAsync(sling.Id, Cancellation);

        await Assert.ThrowsAsync<ApiException>(() => app.SkillsApi(app.Player).UsageAsync(skill.Id, Cancellation));
    }

    [Fact]
    public async Task Writes_leave_snapshot_after_with_author()
    {
        TestDatabase.SkipIfMissing();

        var created = await app.Items().CreateAsync(new ItemDto { Name = Unique("Фонарь"), Type = "Снаряжение" }, Cancellation);
        await app.Items().DeleteAsync(created.Id, Cancellation);

        await using var db = app.Database.CreateContext();
        var log = await db.AuditLog.Where(e => e.EntityId == created.Id).OrderBy(e => e.Id).ToListAsync(Cancellation);
        Assert.Equal([AuditAction.Created, AuditAction.Deleted], log.Select(e => e.Action));
        Assert.All(log, e => Assert.Equal(app.Keeper.Id, e.ActorId));
        Assert.Equal("Снаряжение", log[0].Snapshot!.RootElement.GetProperty("type").GetString());
        Assert.Null(log[1].Snapshot);
    }

    [Fact]
    public async Task Spell_images_keep_order_and_captions_like_weapons()
    {
        TestDatabase.SkipIfMissing();
        Guid sign, gate;
        await using (var db = app.Database.CreateContext())
        {
            var files = new[] { new StoredFile { ExternalUrl = "https://example.test/sign.webp" }, new StoredFile { ExternalUrl = "https://example.test/gate.webp" } };
            db.Files.AddRange(files);
            await db.SaveChangesAsync(Cancellation);
            (sign, gate) = (files[0].Id, files[1].Id);
        }

        var created = await app.Spells().CreateAsync(new SpellDto
        {
            Name = Unique("Знак Старших богов"), SpellType = "Защита",
            Images = [new CatalogImageDto(sign, null, " На камне "), new CatalogImageDto(gate, null, null)],
        }, Cancellation);

        Assert.Equal([sign, gate], created.Images.Select(i => i.FileId));
        Assert.Equal("На камне", created.Images[0].Caption);
        Assert.Equal(FilesRoutes.Content(sign), created.Images[0].Url);

        // Переставили картинки — правка только детей тоже сдвигает версию записи.
        created.Images = [created.Images[1], created.Images[0]];
        var updated = await app.Spells().UpdateAsync(created, Cancellation);
        Assert.Equal([gate, sign], updated.Images.Select(i => i.FileId));
        Assert.NotEqual(created.Version, updated.Version);

        updated.Images = [new CatalogImageDto(Guid.NewGuid(), null, null)];
        var missing = await Assert.ThrowsAsync<ApiException>(() => app.Spells().UpdateAsync(updated, Cancellation));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    /// <summary>Артефакты главы 13: вид и «используют» без повторов, картинки как у заклинаний, файл обмена — по коду.</summary>
    [Fact]
    public async Task Artifact_keeps_kind_owners_and_rule_and_imports_by_code()
    {
        TestDatabase.SkipIfMissing();
        Guid art;
        await using (var db = app.Database.CreateContext())
        {
            var stored = new StoredFile { ExternalUrl = "https://example.test/cube.webp" };
            db.Files.Add(stored);
            await db.SaveChangesAsync(Cancellation);
            art = stored.Id;
        }

        var created = await app.Artifacts().CreateAsync(new ArtifactDto
        {
            Name = Unique("Стазисный куб"), Kind = ArtifactKind.Device, UsedBy = [" йитиане ", "Йитиане", ""], Rule = "  ",
            Description = " Замедляет время. ", Images = [new CatalogImageDto(art, null, "Куб")],
        }, Cancellation);

        Assert.Equal(ArtifactKind.Device, created.Kind);
        Assert.Equal(["йитиане"], created.UsedBy);
        Assert.Null(created.Rule);
        Assert.Equal("Замедляет время.", created.Description);
        Assert.Equal([art], created.Images.Select(i => i.FileId));

        var name = Unique("Сияющий трапецоэдр");
        var file = $$"""{"catalog":"artifacts","items":[{"code":"artifact.shining-trapezohedron","name":"{{name}}","kind":"Relic","usedBy":["культисты Ньярлатхотепа"],"rule":"Закрыть ларец — явится Скиталец Тьмы"}]}""";
        var first = await app.Artifacts().ImportAsync(Json(file), overwrite: true, dryRun: false, Cancellation);
        var again = await app.Artifacts().ImportAsync(Json(file.Replace("Relic", "Device", StringComparison.Ordinal)), overwrite: true, dryRun: false, Cancellation);

        Assert.Equal((1, 0), (first.Created, first.Updated));
        Assert.Equal((0, 1), (again.Created, again.Updated));
        var stone = Assert.Single((await app.Artifacts(app.Player).ListAsync(Cancellation)).Items, a => a.Code == "artifact.shining-trapezohedron");
        Assert.Equal((ArtifactKind.Device, "Закрыть ларец — явится Скиталец Тьмы"), (stone.Kind, stone.Rule));

        var wrong = await Assert.ThrowsAsync<ApiException>(() => app.Artifacts(app.Player).CreateAsync(new ArtifactDto { Name = Unique("Лампа") }, Cancellation));
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
    }

    [Fact]
    public async Task Book_spells_are_matched_to_catalog_on_write()
    {
        TestDatabase.SkipIfMissing();
        var spell = await app.Spells().CreateAsync(new SpellDto
        {
            Name = Unique("Призыв бьякхи"), SpellType = "Призыв", AltNames = ["Зов бьякхи"],
        }, Cancellation);

        var book = await app.Books().CreateAsync(new BookDto
        {
            Name = Unique("Некрономикон"),
            BookType = BookType.MythosBook,
            Spells = [new("Зов бьякхи", null), new("Неведомое заклятие", null)],
        }, Cancellation);

        Assert.Equal(spell.Id, book.Spells[0].SpellId);
        Assert.Null(book.Spells[1].SpellId);
        Assert.Equal("Неведомое заклятие", book.Spells[1].RawName);

        // Правка только детей: строки на месте, лишняя уходит, новая добавляется, версия сдвигается.
        book.Spells = [new("Неведомое заклятие", null), new(spell.Name, null), new("Третье", null)];
        var updated = await app.Books().UpdateAsync(book, Cancellation);
        book.Spells = [new("Третье", null)];
        book.Version = updated.Version;
        var shortened = await app.Books().UpdateAsync(book, Cancellation);

        Assert.Equal(["Неведомое заклятие", spell.Name, "Третье"], updated.Spells.Select(s => s.RawName));
        Assert.Equal(spell.Id, updated.Spells[1].SpellId);
        Assert.NotEqual(book.Version, shortened.Version);
        Assert.Equal(["Третье"], shortened.Spells.Select(s => s.RawName));
    }

    [Fact]
    public async Task Creature_statblock_round_trips_and_import_warns_about_unrollable_damage()
    {
        TestDatabase.SkipIfMissing();
        var creature = await app.Creatures().CreateAsync(new CreatureDto
        {
            Name = Unique("Глубоководный"),
            Type = CreatureType.MythicMonsters,
            Statblock = new Statblock
            {
                Str = new StatValue { Value = 85, Dice = "(3D6+6)×5" },
                HitPoints = 15,
                DamageBonus = "+1D4",
                SanityLoss = "0/1D6",
                Attacks = [new CreatureAttack { Name = "Когти", SkillValue = 45, Damage = "1D6", DamageBonusMode = CreatureDamageBonusMode.Full }],
            },
        }, Cancellation);

        var file = "{\"catalog\":\"creatures\",\"items\":[{\"name\":\"" + Unique("Шоггот")
            + "\",\"statblock\":{\"hitPoints\":0,\"attacks\":[{\"name\":\"Давка\",\"damage\":\"см. описание\"}]}}]}";
        var report = await app.Creatures().ImportAsync(Json(file), overwrite: false, dryRun: true, Cancellation);

        Assert.Equal(85, creature.Statblock.Str.Value);
        Assert.Equal("(3D6+6)×5", creature.Statblock.Str.Dice);
        Assert.Equal(CreatureAttackKind.Melee, creature.Statblock.Attacks[0].Kind);
        Assert.Equal(1, report.Created);
        Assert.Contains(report.Warnings, w => w.Contains("нет ПЗ", StringComparison.Ordinal));
        Assert.Contains(report.Warnings, w => w.Contains("«Давка»", StringComparison.Ordinal));
    }

    // #175: игрок видит название, картинки и «Описание», но не статблок — ни в списке, ни в файле экспорта;
    // у Хранителя статблок на месте. Отсекает сервер, а не интерфейс.
    [Fact]
    public async Task Player_gets_creatures_without_statblock_keeper_with_it()
    {
        TestDatabase.SkipIfMissing();
        var name = Unique("Azathoth");
        var created = await app.Creatures().CreateAsync(new CreatureDto
        {
            Name = name,
            Type = CreatureType.MythicMonsters,
            Description = "Слепой идиот-бог.",
            Statblock = new Statblock
            {
                Str = new StatValue { Value = 85 },
                HitPoints = 15,
                SanityLoss = "1D10/1D100",
                Attacks = [new CreatureAttack { Name = "Claws", SkillValue = 45, Damage = "1D6", DamageBonusMode = CreatureDamageBonusMode.Full }],
            },
        }, Cancellation);

        var asPlayer = await app.Creatures(app.Player).ListAsync(Cancellation);
        var asKeeper = await app.Creatures().ListAsync(Cancellation);

        Assert.All(asPlayer.Items, c => Assert.Equal(new Statblock().HitPoints, c.Statblock.HitPoints));
        Assert.All(asPlayer.Items, c => Assert.Empty(c.Statblock.Attacks));
        var seen = asPlayer.Items.Single(c => c.Id == created.Id);
        Assert.Equal((name, "Слепой идиот-бог."), (seen.Name, seen.Description));
        var full = asKeeper.Items.Single(c => c.Id == created.Id);
        Assert.Equal(15, full.Statblock.HitPoints);
        Assert.Single(full.Statblock.Attacks);

        // Файл экспорта — тот же путь: игрок не получит статблок и им
        var export = await app.CreateClient(app.Player).GetStringAsync(CatalogsRoutes.Creatures.Export, Cancellation);
        Assert.Contains(name, export, StringComparison.Ordinal);
        Assert.DoesNotContain("Claws", export, StringComparison.Ordinal);
        Assert.DoesNotContain("1D10/1D100", export, StringComparison.Ordinal);
        // Ответ Хранителю полный, а ETag у ролей разный: кэш игрока Хранителю не достанется
        Assert.Contains("Claws", await app.CreateClient(app.Keeper).GetStringAsync(CatalogsRoutes.Creatures.Export, Cancellation), StringComparison.Ordinal);
    }

    // #194: неверное значение enum в теле — 400 с понятным текстом, а не 500 (читает тело вручную — тот же обработчик)
    [Fact]
    public async Task Invalid_enum_in_catalog_body_is_400_with_text()
    {
        TestDatabase.SkipIfMissing();

        using var response = await app.CreateClient().PostAsync(CatalogsRoutes.Creatures.Base,
            new StringContent("""{"name":"Тварь","type":"Нет такого","statblock":{}}""", Encoding.UTF8, "application/json"), Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Не читается как JSON", await response.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }

    // ── Обмен JSON ──

    [Fact]
    public async Task Import_skips_or_overwrites_by_name_and_dry_run_writes_nothing()
    {
        TestDatabase.SkipIfMissing();
        var name = Unique("Бинокль");
        await app.Items().CreateAsync(new ItemDto { Name = name, Type = "старый" }, Cancellation);
        var fresh = Unique("Компас");
        var file = $$"""{"catalog":"items","items":[{"name":"{{name.ToUpperInvariant()}}","type":"новый"},{"name":"{{fresh}}"}]}""";

        var dry = await app.Items().ImportAsync(Json(file), overwrite: true, dryRun: true, Cancellation);
        var skip = await app.Items().ImportAsync(Json(file), overwrite: false, dryRun: false, Cancellation);
        var overwrite = await app.Items().ImportAsync(Json(file), overwrite: true, dryRun: false, Cancellation);

        Assert.True(dry.DryRun);
        Assert.Equal((1, 1), (dry.Created, dry.Updated));
        Assert.Equal((1, 1), (skip.Created, skip.Skipped));
        Assert.Equal((0, 2), (overwrite.Created, overwrite.Updated));
        var items = (await app.Items().ListAsync(Cancellation)).Items;
        Assert.Equal("новый", items.Single(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Type);
        Assert.Single(items, i => i.Name == fresh);
    }

    [Fact]
    public async Task Import_of_export_round_trips_by_code()
    {
        TestDatabase.SkipIfMissing();
        var weapon = Weapon("Томпсон");
        weapon.Code = "weapon.thompson";
        var created = await app.Weapons().CreateAsync(weapon, Cancellation);

        using var export = await app.CreateClient().GetAsync(CatalogsRoutes.Weapons.Export, Cancellation);
        var json = await export.Content.ReadAsStringAsync(Cancellation);
        var report = await app.Weapons().ImportAsync(Json(json), overwrite: true, dryRun: true, Cancellation);

        Assert.Equal("attachment", export.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal(0, report.Created);
        Assert.Contains(report.Lines, l => l.Name == created.Name && l.Outcome == CatalogImportOutcome.Updated);
    }

    [Fact]
    public async Task Import_of_other_catalog_file_is_rejected()
    {
        TestDatabase.SkipIfMissing();

        var wrong = await Assert.ThrowsAsync<ApiException>(() =>
            app.Items().ImportAsync(Json("""{"catalog":"weapons","items":[]}"""), overwrite: false, dryRun: true, Cancellation));

        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
    }

    // ── Синхронизация с правилами ──

    [Fact]
    public async Task Occupation_sync_upserts_by_code_and_leaves_homebrew_alone()
    {
        TestDatabase.SkipIfMissing();
        var homebrew = await app.Occupations().CreateAsync(new OccupationDto
        {
            Name = Unique("Охотник на ведьм"),
            SkillPointsFormula = SkillPointsFormula.Edu4,
            CreditRatingMin = 10,
            CreditRatingMax = 30,
            Slots = [new() { Kind = OccupationSlotKind.Free }],
        }, Cancellation);

        var first = await app.Occupations().SyncAsync(dryRun: false, Cancellation);
        var second = await app.Occupations().SyncAsync(dryRun: false, Cancellation);

        Assert.Equal(0, first.Failed);
        Assert.Equal(OccupationSeed.Rows.Count, first.Created + first.Updated);
        Assert.Equal((0, OccupationSeed.Rows.Count), (second.Created, second.Updated));
        var list = (await app.Occupations().ListAsync(Cancellation)).Items;
        var doctor = list.Single(o => o.Code == "occupation.doctor-of-medicine");
        Assert.Equal(8, doctor.Slots.Count);
        Assert.All(list.Where(o => o.Code is not null), o => Assert.Equal(8, o.ProfessionalSkillCount));
        Assert.Contains(doctor.Slots, s => s is { Kind: OccupationSlotKind.Specialization, Specialization: "латынь" });
        Assert.DoesNotContain(list.SelectMany(o => o.Slots), s => s.SkillName == "Средства"); // Средства — не слот
        Assert.All(list.Where(o => o.Code is not null).SelectMany(o => o.Tags), tag => Assert.DoesNotMatch("^[A-Za-z]+$", tag)); // теги русские
        Assert.Equal(homebrew.Version, list.Single(o => o.Id == homebrew.Id).Version);
    }

    [Fact]
    public async Task Sync_report_says_what_the_seed_changed()
    {
        TestDatabase.SkipIfMissing();

        await app.Occupations().SyncAsync(dryRun: false, Cancellation);
        var doctor = (await app.Occupations().ListAsync(Cancellation)).Items.Single(o => o.Code == "occupation.doctor-of-medicine");
        var edited = doctor.CreditRatingMax;
        doctor.CreditRatingMax = edited - 1;
        await app.Occupations().UpdateAsync(doctor, Cancellation);

        var report = await app.Occupations().SyncAsync(dryRun: false, Cancellation);

        var line = report.Lines.Single(l => l.Name == doctor.Name);
        Assert.Equal("изменено: Средства: до", line.Message);
        Assert.Contains(report.Lines, l => l is { Message: "без изменений" });
        Assert.Equal(edited, (await app.Occupations().ListAsync(Cancellation)).Items.Single(o => o.Id == doctor.Id).CreditRatingMax);
    }

    // Картинки профессии — пользовательские, как у бестиария и оружия: сид книги их не знает, и сверка с ним их не стирает.
    [Fact]
    public async Task Occupation_images_keep_order_and_captions_and_survive_sync()
    {
        TestDatabase.SkipIfMissing();
        Guid portrait, desk;
        await using (var db = app.Database.CreateContext())
        {
            var files = new[] { new StoredFile { ExternalUrl = "https://example.test/portrait.webp" }, new StoredFile { ExternalUrl = "https://example.test/desk.webp" } };
            db.Files.AddRange(files);
            await db.SaveChangesAsync(Cancellation);
            (portrait, desk) = (files[0].Id, files[1].Id);
        }

        await app.Occupations().SyncAsync(dryRun: false, Cancellation);
        var antiquarian = (await app.Occupations().ListAsync(Cancellation)).Items.Single(o => o.Code == "occupation.antiquarian");
        antiquarian.Images = [new CatalogImageDto(portrait, null, " В лавке "), new CatalogImageDto(desk, null, null)];
        var updated = await app.Occupations().UpdateAsync(antiquarian, Cancellation);

        Assert.Equal([portrait, desk], updated.Images.Select(i => i.FileId));
        Assert.Equal("В лавке", updated.Images[0].Caption);
        Assert.Equal(FilesRoutes.Content(portrait), updated.Images[0].Url);

        // Обложкой стал стол — правка только детей тоже сдвигает версию записи.
        updated.Images = [updated.Images[1], updated.Images[0]];
        var reordered = await app.Occupations().UpdateAsync(updated, Cancellation);
        Assert.Equal([desk, portrait], reordered.Images.Select(i => i.FileId));
        Assert.NotEqual(updated.Version, reordered.Version);

        var report = await app.Occupations().SyncAsync(dryRun: false, Cancellation);

        Assert.Equal("без изменений", report.Lines.Single(l => l.Name == antiquarian.Name).Message);
        var synced = (await app.Occupations().ListAsync(Cancellation)).Items.Single(o => o.Id == antiquarian.Id);
        Assert.Equal([desk, portrait], synced.Images.Select(i => i.FileId));

        synced.Images = [new CatalogImageDto(Guid.NewGuid(), null, null)];
        var missing = await Assert.ThrowsAsync<ApiException>(() => app.Occupations().UpdateAsync(synced, Cancellation));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task Occupation_slot_must_match_its_kind()
    {
        TestDatabase.SkipIfMissing();

        var invalid = await Assert.ThrowsAsync<ApiException>(() => app.Occupations().CreateAsync(new OccupationDto
        {
            Name = Unique("Сапожник"),
            CreditRatingMin = 9,
            CreditRatingMax = 30,
            Slots = [new() { Kind = OccupationSlotKind.Choice, Options = [app.Skills[SkillCodes.Dodge]] }],
        }, Cancellation));

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private static MemoryStream Json(string json) => new(Encoding.UTF8.GetBytes(json));

    private static WeaponDto Copy(WeaponDto weapon) => System.Text.Json.JsonSerializer.Deserialize(
        System.Text.Json.JsonSerializer.Serialize(weapon, Contracts.ContractsJsonContext.Default.WeaponDto),
        Contracts.ContractsJsonContext.Default.WeaponDto)!;
}
