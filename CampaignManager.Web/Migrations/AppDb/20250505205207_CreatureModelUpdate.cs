#nullable disable

using CampaignManager.Web.Components.Bestiary.Model;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CampaignManager.Web.Migrations.AppDb;

/// <inheritdoc />
public partial class CreatureModelUpdate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            "Abilities",
            schema: "games",
            table: "Creatures");

        migrationBuilder.DropColumn(
            "Categories",
            schema: "games",
            table: "Creatures");

        migrationBuilder.DropColumn(
            "Stats",
            schema: "games",
            table: "Creatures");

        migrationBuilder.AlterColumn<string>(
            "Type",
            schema: "games",
            table: "Creatures",
            type: "text",
            nullable: false,
            defaultValue: "Other",
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AddColumn<Dictionary<string, string>>(
            "CombatDescriptions",
            schema: "games",
            table: "Creatures",
            type: "jsonb",
            nullable: false);

        migrationBuilder.AddColumn<CreatureCharacteristics>(
            "CreatureCharacteristics",
            schema: "games",
            table: "Creatures",
            type: "jsonb",
            nullable: false);

        migrationBuilder.AddColumn<Dictionary<string, string>>(
            "SpecialAbilities",
            schema: "games",
            table: "Creatures",
            type: "jsonb",
            nullable: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            "CombatDescriptions",
            schema: "games",
            table: "Creatures");

        migrationBuilder.DropColumn(
            "CreatureCharacteristics",
            schema: "games",
            table: "Creatures");

        migrationBuilder.DropColumn(
            "SpecialAbilities",
            schema: "games",
            table: "Creatures");

        migrationBuilder.AlterColumn<string>(
            "Type",
            schema: "games",
            table: "Creatures",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldDefaultValue: "Other");

        migrationBuilder.AddColumn<string>(
            "Abilities",
            schema: "games",
            table: "Creatures",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            "Categories",
            schema: "games",
            table: "Creatures",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            "Stats",
            schema: "games",
            table: "Creatures",
            type: "jsonb",
            nullable: true);
    }
}