using System.Collections.Generic;
using CampaignManager.Web.Scenarios.Models;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class CreatureModelUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Abilities",
                schema: "games",
                table: "Creatures");

            migrationBuilder.DropColumn(
                name: "Categories",
                schema: "games",
                table: "Creatures");

            migrationBuilder.DropColumn(
                name: "Stats",
                schema: "games",
                table: "Creatures");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "games",
                table: "Creatures",
                type: "text",
                nullable: false,
                defaultValue: "Other",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<Dictionary<string, string>>(
                name: "CombatDescriptions",
                schema: "games",
                table: "Creatures",
                type: "jsonb",
                nullable: false);

            migrationBuilder.AddColumn<CreatureCharacteristics>(
                name: "CreatureCharacteristics",
                schema: "games",
                table: "Creatures",
                type: "jsonb",
                nullable: false);

            migrationBuilder.AddColumn<Dictionary<string, string>>(
                name: "SpecialAbilities",
                schema: "games",
                table: "Creatures",
                type: "jsonb",
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CombatDescriptions",
                schema: "games",
                table: "Creatures");

            migrationBuilder.DropColumn(
                name: "CreatureCharacteristics",
                schema: "games",
                table: "Creatures");

            migrationBuilder.DropColumn(
                name: "SpecialAbilities",
                schema: "games",
                table: "Creatures");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "games",
                table: "Creatures",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Other");

            migrationBuilder.AddColumn<string>(
                name: "Abilities",
                schema: "games",
                table: "Creatures",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Categories",
                schema: "games",
                table: "Creatures",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Stats",
                schema: "games",
                table: "Creatures",
                type: "jsonb",
                nullable: true);
        }
    }
}
