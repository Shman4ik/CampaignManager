using System.Text.Json;
using CampaignManager.Core;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Encounters;
using CampaignManager.Data;
using CampaignManager.Data.Campaigns;
using CampaignManager.Data.Catalogs;
using CampaignManager.Data.Characters;
using CampaignManager.Data.Encounters;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CampaignManager.Server.Tests.Schema;

/// <summary>
/// Инварианты схемы <c>cm</c>, которые держит база, а не код (docs/v2/SCHEMA.md, правило 2).
/// В v1 «ровно один владелец листа» жил в комментарии и был нарушен в 7 листах из 54.
/// </summary>
public sealed class SchemaConstraintTests(SchemaDatabase db) : IClassFixture<SchemaDatabase>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // Не нужна база: модель и последняя миграция не должны расходиться.
    [Fact]
    public void Migrations_cover_the_model()
    {
        var options = new DbContextOptionsBuilder<CmDbContext>();
        CmDatabase.Configure(options, TestDatabase.Unreachable);
        using var context = new CmDbContext(options.Options);

        Assert.False(context.Database.HasPendingModelChanges(), "Модель изменилась: нужна миграция (dotnet ef migrations add).");
    }

    [Fact]
    public async Task Player_sheet_needs_an_owner()
    {
        TestDatabase.SkipIfMissing();
        await using var context = db.CreateContext();
        context.Characters.Add(new Character { Kind = CharacterKind.Player, Sheet = SchemaDatabase.Sheet("Ничей"), SheetVersion = 1 });

        await AssertViolatesAsync(PostgresErrorCodes.CheckViolation, "ck_characters_owner", context);
    }

    [Fact]
    public async Task Pregen_and_npc_have_no_owner()
    {
        TestDatabase.SkipIfMissing();
        var user = await db.AddUserAsync();
        await using var context = db.CreateContext();
        context.Characters.Add(new Character
        {
            Kind = CharacterKind.Npc, OwnerId = user.Id, Sheet = SchemaDatabase.Sheet("Джон Смит"), SheetVersion = 1,
        });

        await AssertViolatesAsync(PostgresErrorCodes.CheckViolation, "ck_characters_owner", context);
    }

    [Fact]
    public async Task Player_sheet_in_campaign_requires_membership()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await db.AddUserAsync("Хранитель");
        var stranger = await db.AddUserAsync();
        var campaign = await db.AddCampaignAsync(keeper);
        await using var context = db.CreateContext();
        context.Characters.Add(PlayerSheet(stranger.Id, campaign.Id));

        await AssertViolatesAsync(PostgresErrorCodes.ForeignKeyViolation,
            "fk_characters_campaign_members_campaign_id_owner_id", context);
    }

    [Fact]
    public async Task Player_has_one_active_sheet_per_campaign()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await db.AddUserAsync("Хранитель");
        var player = await db.AddUserAsync();
        var campaign = await db.AddCampaignAsync(keeper, player);
        await using (var first = db.CreateContext())
        {
            first.Characters.Add(PlayerSheet(player.Id, campaign.Id));
            first.Characters.Add(PlayerSheet(player.Id, campaign.Id, CharacterStatus.Retired));
            await first.SaveChangesAsync(Cancellation);
        }

        await using var second = db.CreateContext();
        second.Characters.Add(PlayerSheet(player.Id, campaign.Id));

        await AssertViolatesAsync(PostgresErrorCodes.UniqueViolation, "characters_one_active_sheet", second);
    }

    // ON DELETE SET NULL (campaign_id): лист остаётся у игрока, а не пропадает и не теряет владельца.
    [Fact]
    public async Task Removing_member_keeps_sheet_with_owner_outside_campaign()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await db.AddUserAsync("Хранитель");
        var player = await db.AddUserAsync();
        var campaign = await db.AddCampaignAsync(keeper, player);
        var sheet = PlayerSheet(player.Id, campaign.Id);
        await using (var setup = db.CreateContext())
        {
            setup.Characters.Add(sheet);
            await setup.SaveChangesAsync(Cancellation);
        }

        await using (var removal = db.CreateContext())
        {
            await removal.CampaignMembers
                .Where(m => m.CampaignId == campaign.Id && m.UserId == player.Id)
                .ExecuteDeleteAsync(Cancellation);
        }

        await using var check = db.CreateContext();
        var kept = await check.Characters.SingleAsync(c => c.Id == sheet.Id, Cancellation);
        Assert.Equal(player.Id, kept.OwnerId);
        Assert.Null(kept.CampaignId);
        Assert.Equal("Сыщик", kept.Name);
    }

    [Fact]
    public async Task Campaign_has_one_keeper()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await db.AddUserAsync("Хранитель");
        var another = await db.AddUserAsync("Второй Хранитель");
        var campaign = await db.AddCampaignAsync(keeper);
        await using var context = db.CreateContext();
        context.CampaignMembers.Add(new CampaignMember { CampaignId = campaign.Id, UserId = another.Id, Role = CampaignRole.Keeper });

        await AssertViolatesAsync(PostgresErrorCodes.UniqueViolation, "campaign_members_one_keeper", context);
    }

    // Список значений CHECK генерируется из enum: сырой SQL с чужим значением не пройдёт.
    [Fact]
    public async Task Enum_column_is_text_with_generated_check()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await db.AddUserAsync("Хранитель");
        var campaign = await db.AddCampaignAsync(keeper);
        await using var context = db.CreateContext();

        var status = await context.Database
            .SqlQuery<string>($"SELECT status AS \"Value\" FROM cm.campaigns WHERE id = {campaign.Id}")
            .SingleAsync(Cancellation);
        Assert.Equal(nameof(CampaignStatus.Planning), status);

        var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlAsync(
            $"UPDATE cm.campaigns SET status = 'Paused' WHERE id = {campaign.Id}", Cancellation));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_campaigns_status", error.ConstraintName);
    }

    [Fact]
    public async Task Catalog_name_is_unique_ignoring_case()
    {
        TestDatabase.SkipIfMissing();
        var name = $"Наука ({Guid.NewGuid():N})";
        await using (var first = db.CreateContext())
        {
            first.Skills.Add(new Skill { Name = name, Category = SkillCategory.Knowledge, Eras = [Era.Classic] });
            await first.SaveChangesAsync(Cancellation);

            var stored = await first.Skills.AsNoTracking().SingleAsync(s => s.Name == name, Cancellation);
            Assert.Equal([Era.Classic], stored.Eras);
        }

        await using var second = db.CreateContext();
        second.Skills.Add(new Skill { Name = name.ToUpperInvariant(), Category = SkillCategory.Knowledge });

        await AssertViolatesAsync(PostgresErrorCodes.UniqueViolation, "skills_name", second);
    }

    // NULLS NOT DISTINCT: и вне кампании у Хранителя одна активная сцена каждого вида.
    [Fact]
    public async Task Keeper_has_one_active_encounter_even_outside_campaign()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await db.AddUserAsync("Хранитель");
        await using (var first = db.CreateContext())
        {
            first.Encounters.Add(Encounter(keeper.Id));
            first.Encounters.Add(Encounter(keeper.Id, EncounterStatus.Finished));
            await first.SaveChangesAsync(Cancellation);
        }

        await using var second = db.CreateContext();
        second.Encounters.Add(Encounter(keeper.Id));

        await AssertViolatesAsync(PostgresErrorCodes.UniqueViolation, "encounters_one_active", second);
    }

    // xmin как токен: правка с устаревшей версией — конфликт, а не молчаливая перезапись.
    [Fact]
    public async Task Stale_sheet_update_is_a_concurrency_conflict()
    {
        TestDatabase.SkipIfMissing();
        var sheet = new Character { Kind = CharacterKind.Npc, Sheet = SchemaDatabase.Sheet("Уильям Уильямс"), SheetVersion = 1 };
        await using (var setup = db.CreateContext())
        {
            setup.Characters.Add(sheet);
            await setup.SaveChangesAsync(Cancellation);
        }

        await using var tablet = db.CreateContext();
        await using var phone = db.CreateContext();
        var onTablet = await tablet.Characters.SingleAsync(c => c.Id == sheet.Id, Cancellation);
        var onPhone = await phone.Characters.SingleAsync(c => c.Id == sheet.Id, Cancellation);

        onTablet.Status = CharacterStatus.Archived;
        await tablet.SaveChangesAsync(Cancellation);

        onPhone.Status = CharacterStatus.Inactive;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => phone.SaveChangesAsync(Cancellation));
    }

    [Fact]
    public async Task Timestamps_are_set_on_insert_and_update()
    {
        TestDatabase.SkipIfMissing();
        var time = new ManualTime { Now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero) };
        var campaign = new Campaign { Name = "Ужас Данвича" };
        await using (var insert = db.CreateContext(time))
        {
            insert.Campaigns.Add(campaign);
            await insert.SaveChangesAsync(Cancellation);
        }

        time.Now = time.Now.AddHours(1);
        await using (var update = db.CreateContext(time))
        {
            var stored = await update.Campaigns.SingleAsync(c => c.Id == campaign.Id, Cancellation);
            stored.Status = CampaignStatus.Active;
            await update.SaveChangesAsync(Cancellation);
        }

        await using var check = db.CreateContext();
        var result = await check.Campaigns.SingleAsync(c => c.Id == campaign.Id, Cancellation);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero), result.CreatedAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 13, 0, 0, TimeSpan.Zero), result.UpdatedAt);
    }

    [Fact]
    public async Task Weapon_numbers_and_shotgun_ranges_round_trip()
    {
        TestDatabase.SkipIfMissing();
        var skill = new Skill { Name = $"Стрельба ({Guid.NewGuid():N})", Category = SkillCategory.CombatFirearms };
        var weapon = new Weapon
        {
            Name = $"Дробовик ({Guid.NewGuid():N})",
            Type = WeaponType.Shotguns,
            SkillId = skill.Id,
            Damage = "4d6/2d6/1d6",
            DamageByRange = [new("10 м", "4d6"), new("20 м", "2d6")],
            AmmoCapacityOptions = [2, 5],
            Malfunction = 100,
            CostClassic = 40.5m,
        };
        await using (var insert = db.CreateContext())
        {
            insert.AddRange(skill, weapon);
            await insert.SaveChangesAsync(Cancellation);
        }

        await using var check = db.CreateContext();
        var stored = await check.Weapons.SingleAsync(w => w.Id == weapon.Id, Cancellation);
        Assert.Equal(weapon.DamageByRange, stored.DamageByRange);
        Assert.Equal([2, 5], Assert.IsType<int[]>(stored.AmmoCapacityOptions));
        Assert.Equal(40.5m, stored.CostClassic);
        Assert.Equal([Era.Classic, Era.Modern], stored.Eras);

        var json = await check.Database
            .SqlQuery<string>($"SELECT damage_by_range::text AS \"Value\" FROM cm.weapons WHERE id = {weapon.Id}")
            .SingleAsync(Cancellation);
        Assert.Equal("10 м", JsonDocument.Parse(json).RootElement[0].GetProperty("range").GetString());
    }

    private static Character PlayerSheet(Guid ownerId, Guid campaignId, CharacterStatus status = CharacterStatus.Active) => new()
    {
        Kind = CharacterKind.Player,
        Status = status,
        OwnerId = ownerId,
        CampaignId = campaignId,
        Sheet = SchemaDatabase.Sheet("Сыщик"),
        SheetVersion = 1,
    };

    private static Encounter Encounter(Guid keeperId, EncounterStatus status = EncounterStatus.Active) => new()
    {
        KeeperId = keeperId,
        Kind = EncounterKind.Combat,
        Status = status,
        State = JsonDocument.Parse("{}"),
        StateVersion = 1,
    };

    private static async Task AssertViolatesAsync(string sqlState, string constraint, CmDbContext context)
    {
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Cancellation));
        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(sqlState, postgres.SqlState);
        Assert.Equal(constraint, postgres.ConstraintName);
    }

    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
