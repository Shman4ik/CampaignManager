using System.Collections.Generic;
using CampaignManager.Web.Components.Features.Scenarios.Model;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class ScenarioStructuredContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ICollection<ScenarioHandout>>(
                name: "Handouts",
                schema: "games",
                table: "Scenarios",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'");

            migrationBuilder.AddColumn<ICollection<ScenarioKeyFact>>(
                name: "KeyFacts",
                schema: "games",
                table: "Scenarios",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'");

            migrationBuilder.AddColumn<ICollection<ScenarioLocation>>(
                name: "Locations",
                schema: "games",
                table: "Scenarios",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Handouts",
                schema: "games",
                table: "Scenarios");

            migrationBuilder.DropColumn(
                name: "KeyFacts",
                schema: "games",
                table: "Scenarios");

            migrationBuilder.DropColumn(
                name: "Locations",
                schema: "games",
                table: "Scenarios");
        }
    }
}
