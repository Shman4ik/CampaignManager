using System;
using System.Collections.Generic;
using CampaignManager.Web.Components.Features.Bestiary.Model;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Chase.Model;
using CampaignManager.Web.Components.Features.Scenarios.Model;
using CampaignManager.Web.Components.Features.Weapons.Model;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "games");

            migrationBuilder.CreateTable(
                name: "Books",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    BookType = table.Column<string>(type: "text", nullable: false),
                    AlternativeNames = table.Column<string>(type: "jsonb", nullable: false),
                    Language = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Year = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Author = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SanityLoss = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CthulhuMythosInitial = table.Column<int>(type: "integer", nullable: true),
                    CthulhuMythosFull = table.Column<int>(type: "integer", nullable: true),
                    MythosRating = table.Column<int>(type: "integer", nullable: true),
                    StudyWeeks = table.Column<int>(type: "integer", nullable: true),
                    OccultismBonus = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    PossibleSpells = table.Column<string>(type: "jsonb", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Books", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Campaigns",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Planning"),
                    KeeperEmail = table.Column<string>(type: "text", nullable: true),
                    Era = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChaseSessions",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    KeeperEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    State = table.Column<ChaseSnapshot>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChaseSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Creatures",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false, defaultValue: "Other"),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatureCharacteristics = table.Column<CreatureCharacteristics>(type: "jsonb", nullable: false),
                    Attacks = table.Column<List<CreatureAttack>>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    CombatDescriptions = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    Skills = table.Column<List<CreatureSkill>>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    SpecialAbilities = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Creatures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EditHistoryEntries",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    EditorEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EditorName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: true),
                    PreviousSnapshotJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditHistoryEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Era = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KeeperApplications",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ReviewedByEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewComment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeeperApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LlmKnowledgeEntries",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmKnowledgeEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Occupations",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SkillPointFormula = table.Column<string>(type: "text", nullable: false),
                    CreditRatingMin = table.Column<int>(type: "integer", nullable: false),
                    CreditRatingMax = table.Column<int>(type: "integer", nullable: false),
                    OccupationSkills = table.Column<string>(type: "jsonb", nullable: false),
                    FreeSkillSlots = table.Column<int>(type: "integer", nullable: false),
                    SocialSkillSlots = table.Column<int>(type: "integer", nullable: false),
                    IsModern = table.Column<bool>(type: "boolean", nullable: false),
                    IsLovecraftian = table.Column<bool>(type: "boolean", nullable: false),
                    Tags = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Occupations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BaseValue = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    IsUncommon = table.Column<bool>(type: "boolean", nullable: false),
                    UsageExamples = table.Column<string>(type: "jsonb", nullable: false),
                    FailureConsequences = table.Column<string>(type: "jsonb", nullable: false),
                    TimeRequired = table.Column<string>(type: "text", nullable: false),
                    CanRetry = table.Column<bool>(type: "boolean", nullable: false),
                    OpposingSkills = table.Column<string>(type: "jsonb", nullable: false),
                    ParentSkillId = table.Column<Guid>(type: "uuid", nullable: true),
                    Is1920 = table.Column<bool>(type: "boolean", nullable: false),
                    IsModern = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Skills_Skills_ParentSkillId",
                        column: x => x.ParentSkillId,
                        principalSchema: "games",
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Spells",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Cost = table.Column<string>(type: "text", nullable: true),
                    CastingTime = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    AlternativeNames = table.Column<string>(type: "jsonb", nullable: false),
                    SpellType = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Spells", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserPreferences",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserEmail = table.Column<string>(type: "text", nullable: false),
                    Preferences = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Weapons",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false, defaultValue: "Melee"),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Skill = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Is1920 = table.Column<bool>(type: "boolean", nullable: false),
                    IsModern = table.Column<bool>(type: "boolean", nullable: false),
                    IsRare = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "Редкое оружие: колонка «Встречается» таблицы XVII"),
                    Damage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Range = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Attacks = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Cost = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Ammo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Malfunction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IsImpaling = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DamageInfo = table.Column<WeaponDamageInfo>(type: "jsonb", nullable: true, comment: "Структурированная информация об уроне (авто-парсинг поля Damage)"),
                    RangeInfo = table.Column<WeaponRangeInfo>(type: "jsonb", nullable: true, comment: "Структурированная дальность (авто-парсинг поля Range)"),
                    AttacksInfo = table.Column<WeaponAttacksInfo>(type: "jsonb", nullable: true, comment: "Структурированное число атак (авто-парсинг поля Attacks)"),
                    AmmoInfo = table.Column<WeaponAmmoInfo>(type: "jsonb", nullable: true, comment: "Структурированный боезапас (авто-парсинг поля Ammo)"),
                    CostInfo = table.Column<WeaponCostInfo>(type: "jsonb", nullable: true, comment: "Структурированная стоимость (авто-парсинг поля Cost)"),
                    MalfunctionThreshold = table.Column<int>(type: "integer", nullable: true, comment: "Порог осечки числом (авто-парсинг поля Malfunction)"),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: true, comment: "Навык из справочника Skills; без FK — см. Weapon.SkillId"),
                    CatalogWeaponId = table.Column<Guid>(type: "uuid", nullable: true, comment: "Заполнено только у копии в листе персонажа; у каталожной строки — null"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Weapons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CampaignPlayers",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerEmail = table.Column<string>(type: "text", nullable: false),
                    PlayerName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignPlayers_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "games",
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Scenarios",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Location = table.Column<string>(type: "text", nullable: true),
                    Era = table.Column<string>(type: "text", nullable: true),
                    Journal = table.Column<string>(type: "text", nullable: true),
                    IsTemplate = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ScheduledDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AnnouncementText = table.Column<string>(type: "text", nullable: true),
                    CreatorEmail = table.Column<string>(type: "text", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScenarioCreatures = table.Column<ICollection<ScenarioCreature>>(type: "jsonb", nullable: false),
                    ScenarioItems = table.Column<ICollection<ScenarioItem>>(type: "jsonb", nullable: false),
                    Locations = table.Column<ICollection<ScenarioLocation>>(type: "jsonb", nullable: false),
                    KeyFacts = table.Column<ICollection<ScenarioKeyFact>>(type: "jsonb", nullable: false),
                    Handouts = table.Column<ICollection<ScenarioHandout>>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scenarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Scenarios_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "games",
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Characters",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Character = table.Column<Character>(type: "jsonb", nullable: false, comment: "JSON-представление персонажа со всеми характеристиками"),
                    CampaignPlayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScenarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Characters_CampaignPlayers_CampaignPlayerId",
                        column: x => x.CampaignPlayerId,
                        principalSchema: "games",
                        principalTable: "CampaignPlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Characters_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "games",
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Characters_Scenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalSchema: "games",
                        principalTable: "Scenarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ScenarioNpcs",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScenarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioNpcs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScenarioNpcs_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalSchema: "games",
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScenarioNpcs_Scenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalSchema: "games",
                        principalTable: "Scenarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Books_Name",
                schema: "games",
                table: "Books",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPlayers_CampaignId",
                schema: "games",
                table: "CampaignPlayers",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPlayers_PlayerEmail_CampaignId",
                schema: "games",
                table: "CampaignPlayers",
                columns: new[] { "PlayerEmail", "CampaignId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_KeeperEmail",
                schema: "games",
                table: "Campaigns",
                column: "KeeperEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_CampaignId",
                schema: "games",
                table: "Characters",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_CampaignPlayerId",
                schema: "games",
                table: "Characters",
                column: "CampaignPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_Kind",
                schema: "games",
                table: "Characters",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_ScenarioId",
                schema: "games",
                table: "Characters",
                column: "ScenarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ChaseSessions_KeeperEmail_CampaignId",
                schema: "games",
                table: "ChaseSessions",
                columns: new[] { "KeeperEmail", "CampaignId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Creatures_Name",
                schema: "games",
                table: "Creatures",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EditHistoryEntries_EntityType_EntityId_CreatedAt",
                schema: "games",
                table: "EditHistoryEntries",
                columns: new[] { "EntityType", "EntityId", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Items_Name",
                schema: "games",
                table: "Items",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KeeperApplications_UserEmail_Status",
                schema: "games",
                table: "KeeperApplications",
                columns: new[] { "UserEmail", "Status" },
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_LlmKnowledgeEntries_Key",
                schema: "games",
                table: "LlmKnowledgeEntries",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Occupations_Name",
                schema: "games",
                table: "Occupations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioNpcs_CharacterId",
                schema: "games",
                table: "ScenarioNpcs",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioNpcs_ScenarioId_CharacterId",
                schema: "games",
                table: "ScenarioNpcs",
                columns: new[] { "ScenarioId", "CharacterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Scenarios_CampaignId",
                schema: "games",
                table: "Scenarios",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Scenarios_CreatorEmail",
                schema: "games",
                table: "Scenarios",
                column: "CreatorEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_Name",
                schema: "games",
                table: "Skills",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Skills_ParentSkillId",
                schema: "games",
                table: "Skills",
                column: "ParentSkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Spells_Name",
                schema: "games",
                table: "Spells",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_UserEmail",
                schema: "games",
                table: "UserPreferences",
                column: "UserEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Weapons_Name",
                schema: "games",
                table: "Weapons",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Books",
                schema: "games");

            migrationBuilder.DropTable(
                name: "ChaseSessions",
                schema: "games");

            migrationBuilder.DropTable(
                name: "Creatures",
                schema: "games");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys",
                schema: "games");

            migrationBuilder.DropTable(
                name: "EditHistoryEntries",
                schema: "games");

            migrationBuilder.DropTable(
                name: "Items",
                schema: "games");

            migrationBuilder.DropTable(
                name: "KeeperApplications",
                schema: "games");

            migrationBuilder.DropTable(
                name: "LlmKnowledgeEntries",
                schema: "games");

            migrationBuilder.DropTable(
                name: "Occupations",
                schema: "games");

            migrationBuilder.DropTable(
                name: "ScenarioNpcs",
                schema: "games");

            migrationBuilder.DropTable(
                name: "Skills",
                schema: "games");

            migrationBuilder.DropTable(
                name: "Spells",
                schema: "games");

            migrationBuilder.DropTable(
                name: "UserPreferences",
                schema: "games");

            migrationBuilder.DropTable(
                name: "Weapons",
                schema: "games");

            migrationBuilder.DropTable(
                name: "Characters",
                schema: "games");

            migrationBuilder.DropTable(
                name: "CampaignPlayers",
                schema: "games");

            migrationBuilder.DropTable(
                name: "Scenarios",
                schema: "games");

            migrationBuilder.DropTable(
                name: "Campaigns",
                schema: "games");
        }
    }
}
