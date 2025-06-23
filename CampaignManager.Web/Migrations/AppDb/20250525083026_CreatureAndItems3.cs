using System.Collections.Generic;
using CampaignManager.Web.Components.Features.Scenarios.Model;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class CreatureAndItems3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<ICollection<ScenarioItem>>(
                name: "ScenarioItems",
                schema: "games",
                table: "Scenarios",
                type: "jsonb",
                nullable: false,
                defaultValue: new List<ScenarioItem>(),
                oldClrType: typeof(ICollection<ScenarioItem>),
                oldType: "jsonb",
                oldDefaultValue: new List<ScenarioItem>());

            migrationBuilder.AlterColumn<ICollection<ScenarioCreature>>(
                name: "ScenarioCreatures",
                schema: "games",
                table: "Scenarios",
                type: "jsonb",
                nullable: false,
                defaultValue: new List<ScenarioCreature>(),
                oldClrType: typeof(ICollection<ScenarioCreature>),
                oldType: "jsonb",
                oldDefaultValue: new List<ScenarioCreature>());

            migrationBuilder.AlterColumn<string>(
                name: "Era",
                schema: "games",
                table: "Items",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Classic");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<ICollection<ScenarioItem>>(
                name: "ScenarioItems",
                schema: "games",
                table: "Scenarios",
                type: "jsonb",
                nullable: false,
                defaultValue: new List<ScenarioItem>(),
                oldClrType: typeof(ICollection<ScenarioItem>),
                oldType: "jsonb",
                oldDefaultValue: new List<ScenarioItem>());

            migrationBuilder.AlterColumn<ICollection<ScenarioCreature>>(
                name: "ScenarioCreatures",
                schema: "games",
                table: "Scenarios",
                type: "jsonb",
                nullable: false,
                defaultValue: new List<ScenarioCreature>(),
                oldClrType: typeof(ICollection<ScenarioCreature>),
                oldType: "jsonb",
                oldDefaultValue: new List<ScenarioCreature>());

            migrationBuilder.AlterColumn<string>(
                name: "Era",
                schema: "games",
                table: "Items",
                type: "text",
                nullable: false,
                defaultValue: "Classic",
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}