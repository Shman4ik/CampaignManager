#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace CampaignManager.Web.Migrations.AppDb;

/// <inheritdoc />
public partial class Weapons : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "CloseCombatWeapons",
            "games");

        migrationBuilder.DropTable(
            "RangedCombatWeapons",
            "games");

        migrationBuilder.CreateTable(
            "Weapons",
            schema: "games",
            columns: table => new
            {
                Id = table.Column<Guid>("uuid", nullable: false),
                Type = table.Column<string>("text", nullable: false, defaultValue: "Melee"),
                Name = table.Column<string>("character varying(100)", maxLength: 100, nullable: false),
                Skill = table.Column<string>("character varying(50)", maxLength: 50, nullable: false),
                Is1920 = table.Column<bool>("boolean", nullable: false),
                IsModern = table.Column<bool>("boolean", nullable: false),
                Damage = table.Column<string>("character varying(50)", maxLength: 50, nullable: false),
                Range = table.Column<string>("character varying(50)", maxLength: 50, nullable: false),
                Attacks = table.Column<string>("character varying(20)", maxLength: 20, nullable: false),
                Cost = table.Column<string>("character varying(20)", maxLength: 20, nullable: false),
                Notes = table.Column<string>("character varying(500)", maxLength: 500, nullable: false),
                Ammo = table.Column<string>("character varying(20)", maxLength: 20, nullable: false),
                Malfunction = table.Column<string>("character varying(10)", maxLength: 10, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
                LastUpdated = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false)
            },
            constraints: table => { table.PrimaryKey("PK_Weapons", x => x.Id); });

        migrationBuilder.CreateIndex(
            "IX_Weapons_Name",
            schema: "games",
            table: "Weapons",
            column: "Name",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "Weapons",
            "games");

        migrationBuilder.CreateTable(
            "CloseCombatWeapons",
            schema: "games",
            columns: table => new
            {
                Id = table.Column<Guid>("uuid", nullable: false),
                Attacks = table.Column<string>("character varying(20)", maxLength: 20, nullable: false),
                Cost = table.Column<string>("character varying(20)", maxLength: 20, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
                Damage = table.Column<string>("character varying(50)", maxLength: 50, nullable: false),
                LastUpdated = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
                Name = table.Column<string>("character varying(100)", maxLength: 100, nullable: false),
                Notes = table.Column<string>("character varying(500)", maxLength: 500, nullable: false),
                Range = table.Column<string>("character varying(50)", maxLength: 50, nullable: false),
                Skill = table.Column<string>("character varying(50)", maxLength: 50, nullable: false),
                Type = table.Column<string>("text", nullable: false, defaultValue: "CloseCombat")
            },
            constraints: table => { table.PrimaryKey("PK_CloseCombatWeapons", x => x.Id); });

        migrationBuilder.CreateTable(
            "RangedCombatWeapons",
            schema: "games",
            columns: table => new
            {
                Id = table.Column<Guid>("uuid", nullable: false),
                Ammo = table.Column<string>("character varying(20)", maxLength: 20, nullable: false),
                Attacks = table.Column<string>("character varying(20)", maxLength: 20, nullable: false),
                Cost = table.Column<string>("character varying(20)", maxLength: 20, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
                Damage = table.Column<string>("character varying(50)", maxLength: 50, nullable: false),
                LastUpdated = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
                Malfunction = table.Column<string>("character varying(10)", maxLength: 10, nullable: false),
                Name = table.Column<string>("character varying(100)", maxLength: 100, nullable: false),
                Notes = table.Column<string>("character varying(500)", maxLength: 500, nullable: false),
                Range = table.Column<string>("character varying(50)", maxLength: 50, nullable: false),
                Skill = table.Column<string>("character varying(50)", maxLength: 50, nullable: false),
                Type = table.Column<string>("text", nullable: false, defaultValue: "RangedCombat")
            },
            constraints: table => { table.PrimaryKey("PK_RangedCombatWeapons", x => x.Id); });

        migrationBuilder.CreateIndex(
            "IX_CloseCombatWeapons_Name",
            schema: "games",
            table: "CloseCombatWeapons",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_RangedCombatWeapons_Name",
            schema: "games",
            table: "RangedCombatWeapons",
            column: "Name",
            unique: true);
    }
}