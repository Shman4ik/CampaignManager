using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddWeaponsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CloseCombatWeapons",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Skill = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Damage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Range = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Attacks = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Cost = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false, defaultValue: "CloseCombat")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CloseCombatWeapons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RangedCombatWeapons",
                schema: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Ammo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Malfunction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Skill = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Damage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Range = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Attacks = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Cost = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false, defaultValue: "RangedCombat")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RangedCombatWeapons", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CloseCombatWeapons_Name",
                schema: "games",
                table: "CloseCombatWeapons",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RangedCombatWeapons_Name",
                schema: "games",
                table: "RangedCombatWeapons",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CloseCombatWeapons",
                schema: "games");

            migrationBuilder.DropTable(
                name: "RangedCombatWeapons",
                schema: "games");
        }
    }
}
