using System.Collections.Generic;
using CampaignManager.Web.Components.Features.Scenarios.Model;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class RemoveScenarioDefaultValues : Migration
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
                oldClrType: typeof(ICollection<ScenarioItem>),
                oldType: "jsonb",
                oldDefaultValue: new List<ScenarioItem>());

            migrationBuilder.AlterColumn<ICollection<ScenarioCreature>>(
                name: "ScenarioCreatures",
                schema: "games",
                table: "Scenarios",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(ICollection<ScenarioCreature>),
                oldType: "jsonb",
                oldDefaultValue: new List<ScenarioCreature>());
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
                oldType: "jsonb");

            migrationBuilder.AlterColumn<ICollection<ScenarioCreature>>(
                name: "ScenarioCreatures",
                schema: "games",
                table: "Scenarios",
                type: "jsonb",
                nullable: false,
                defaultValue: new List<ScenarioCreature>(),
                oldClrType: typeof(ICollection<ScenarioCreature>),
                oldType: "jsonb");
        }
    }
}
