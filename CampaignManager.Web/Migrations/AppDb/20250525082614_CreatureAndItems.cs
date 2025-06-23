using System;
using System.Collections.Generic;
using CampaignManager.Web.Components.Features.Scenarios.Model;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class CreatureAndItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScenarioCreatures",
                schema: "games");

            migrationBuilder.DropTable(
                name: "ScenarioItems",
                schema: "games");

            migrationBuilder.DropColumn(
                name: "Categories",
                schema: "games",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Effects",
                schema: "games",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Rarity",
                schema: "games",
                table: "Items");

            migrationBuilder.AddColumn<ICollection<ScenarioCreature>>(
                name: "ScenarioCreatures",
                schema: "games",
                table: "Scenarios",
                type: "jsonb",
                nullable: false,
                defaultValue: new List<ScenarioCreature>());

            migrationBuilder.AddColumn<ICollection<ScenarioItem>>(
                name: "ScenarioItems",
                schema: "games",
                table: "Scenarios",
                type: "jsonb",
                nullable: false,
                defaultValue: new List<ScenarioItem>());

            migrationBuilder.AddColumn<int>(
                name: "Era",
                schema: "games",
                table: "Items",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScenarioCreatures",
                schema: "games",
                table: "Scenarios");

            migrationBuilder.DropColumn(
                name: "ScenarioItems",
                schema: "games",
                table: "Scenarios");

            migrationBuilder.DropColumn(
                name: "Era",
                schema: "games",
                table: "Items");

            migrationBuilder.AddColumn<string>(
                name: "Categories",
                schema: "games",
                table: "Items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Effects",
                schema: "games",
                table: "Items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rarity",
                schema: "games",
                table: "Items",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ScenarioCreatures",
                schema: "games",
                columns: table => new
                {
                    ScenarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioCreatures", x => new { x.ScenarioId, x.CreatureId, x.Id });
                    table.ForeignKey(
                        name: "FK_ScenarioCreatures_Creatures_CreatureId",
                        column: x => x.CreatureId,
                        principalSchema: "games",
                        principalTable: "Creatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScenarioCreatures_Scenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalSchema: "games",
                        principalTable: "Scenarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScenarioItems",
                schema: "games",
                columns: table => new
                {
                    ScenarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioItems", x => new { x.ScenarioId, x.ItemId, x.Id });
                    table.ForeignKey(
                        name: "FK_ScenarioItems_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "games",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScenarioItems_Scenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalSchema: "games",
                        principalTable: "Scenarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioCreatures_CreatureId",
                schema: "games",
                table: "ScenarioCreatures",
                column: "CreatureId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioItems_ItemId",
                schema: "games",
                table: "ScenarioItems",
                column: "ItemId");
        }
    }
}