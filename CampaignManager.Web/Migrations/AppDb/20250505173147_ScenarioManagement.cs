#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace CampaignManager.Web.Migrations.AppDb;

/// <inheritdoc />
public partial class ScenarioManagement : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "Creatures",
            schema: "games",
            columns: table => new
            {
                Id = table.Column<Guid>("uuid", nullable: false),
                Name = table.Column<string>("text", nullable: false),
                Type = table.Column<string>("text", nullable: true),
                Description = table.Column<string>("text", nullable: true),
                Stats = table.Column<string>("jsonb", nullable: true),
                Abilities = table.Column<string>("text", nullable: true),
                ImageUrl = table.Column<string>("text", nullable: true),
                Categories = table.Column<string>("text", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
                LastUpdated = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_Creatures", x => x.Id); });

        migrationBuilder.CreateTable(
            "Items",
            schema: "games",
            columns: table => new
            {
                Id = table.Column<Guid>("uuid", nullable: false),
                Name = table.Column<string>("text", nullable: false),
                Type = table.Column<string>("text", nullable: true),
                Description = table.Column<string>("text", nullable: true),
                Effects = table.Column<string>("text", nullable: true),
                Rarity = table.Column<string>("text", nullable: true),
                ImageUrl = table.Column<string>("text", nullable: true),
                Categories = table.Column<string>("text", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
                LastUpdated = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_Items", x => x.Id); });

        migrationBuilder.CreateTable(
            "Scenarios",
            schema: "games",
            columns: table => new
            {
                Id = table.Column<Guid>("uuid", nullable: false),
                Name = table.Column<string>("text", nullable: false),
                Description = table.Column<string>("text", nullable: true),
                Location = table.Column<string>("text", nullable: true),
                Era = table.Column<string>("text", nullable: true),
                Journal = table.Column<string>("text", nullable: true),
                IsTemplate = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                CreatorEmail = table.Column<string>("text", nullable: true),
                CampaignId = table.Column<Guid>("uuid", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
                LastUpdated = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Scenarios", x => x.Id);
                table.ForeignKey(
                    "FK_Scenarios_Campaigns_CampaignId",
                    x => x.CampaignId,
                    principalSchema: "games",
                    principalTable: "Campaigns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            "ScenarioCreatures",
            schema: "games",
            columns: table => new
            {
                Id = table.Column<Guid>("uuid", nullable: false),
                ScenarioId = table.Column<Guid>("uuid", nullable: false),
                CreatureId = table.Column<Guid>("uuid", nullable: false),
                Location = table.Column<string>("text", nullable: true),
                Notes = table.Column<string>("text", nullable: true),
                Quantity = table.Column<int>("integer", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
                LastUpdated = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ScenarioCreatures", x => new { x.ScenarioId, x.CreatureId, x.Id });
                table.ForeignKey(
                    "FK_ScenarioCreatures_Creatures_CreatureId",
                    x => x.CreatureId,
                    principalSchema: "games",
                    principalTable: "Creatures",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    "FK_ScenarioCreatures_Scenarios_ScenarioId",
                    x => x.ScenarioId,
                    principalSchema: "games",
                    principalTable: "Scenarios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "ScenarioItems",
            schema: "games",
            columns: table => new
            {
                Id = table.Column<Guid>("uuid", nullable: false),
                ScenarioId = table.Column<Guid>("uuid", nullable: false),
                ItemId = table.Column<Guid>("uuid", nullable: false),
                Location = table.Column<string>("text", nullable: true),
                Notes = table.Column<string>("text", nullable: true),
                Quantity = table.Column<int>("integer", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
                LastUpdated = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ScenarioItems", x => new { x.ScenarioId, x.ItemId, x.Id });
                table.ForeignKey(
                    "FK_ScenarioItems_Items_ItemId",
                    x => x.ItemId,
                    principalSchema: "games",
                    principalTable: "Items",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    "FK_ScenarioItems_Scenarios_ScenarioId",
                    x => x.ScenarioId,
                    principalSchema: "games",
                    principalTable: "Scenarios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "ScenarioNpcs",
            schema: "games",
            columns: table => new
            {
                Id = table.Column<Guid>("uuid", nullable: false),
                Name = table.Column<string>("text", nullable: false),
                Description = table.Column<string>("text", nullable: true),
                Role = table.Column<string>("text", nullable: true),
                Location = table.Column<string>("text", nullable: true),
                Notes = table.Column<string>("text", nullable: true),
                CharacterId = table.Column<Guid>("uuid", nullable: true),
                ScenarioId = table.Column<Guid>("uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
                LastUpdated = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ScenarioNpcs", x => x.Id);
                table.ForeignKey(
                    "FK_ScenarioNpcs_Characters_CharacterId",
                    x => x.CharacterId,
                    principalSchema: "games",
                    principalTable: "Characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    "FK_ScenarioNpcs_Scenarios_ScenarioId",
                    x => x.ScenarioId,
                    principalSchema: "games",
                    principalTable: "Scenarios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            "IX_Creatures_Name",
            schema: "games",
            table: "Creatures",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_Items_Name",
            schema: "games",
            table: "Items",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_ScenarioCreatures_CreatureId",
            schema: "games",
            table: "ScenarioCreatures",
            column: "CreatureId");

        migrationBuilder.CreateIndex(
            "IX_ScenarioItems_ItemId",
            schema: "games",
            table: "ScenarioItems",
            column: "ItemId");

        migrationBuilder.CreateIndex(
            "IX_ScenarioNpcs_CharacterId",
            schema: "games",
            table: "ScenarioNpcs",
            column: "CharacterId",
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_ScenarioNpcs_ScenarioId",
            schema: "games",
            table: "ScenarioNpcs",
            column: "ScenarioId");

        migrationBuilder.CreateIndex(
            "IX_Scenarios_CampaignId",
            schema: "games",
            table: "Scenarios",
            column: "CampaignId");

        migrationBuilder.CreateIndex(
            "IX_Scenarios_CreatorEmail",
            schema: "games",
            table: "Scenarios",
            column: "CreatorEmail");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "ScenarioCreatures",
            "games");

        migrationBuilder.DropTable(
            "ScenarioItems",
            "games");

        migrationBuilder.DropTable(
            "ScenarioNpcs",
            "games");

        migrationBuilder.DropTable(
            "Creatures",
            "games");

        migrationBuilder.DropTable(
            "Items",
            "games");

        migrationBuilder.DropTable(
            "Scenarios",
            "games");
    }
}