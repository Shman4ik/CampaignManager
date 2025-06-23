using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class CharacterTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ScenarioId",
                schema: "games",
                table: "Characters",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Characters_ScenarioId",
                schema: "games",
                table: "Characters",
                column: "ScenarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Scenarios_ScenarioId",
                schema: "games",
                table: "Characters",
                column: "ScenarioId",
                principalSchema: "games",
                principalTable: "Scenarios",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Scenarios_ScenarioId",
                schema: "games",
                table: "Characters");

            migrationBuilder.DropIndex(
                name: "IX_Characters_ScenarioId",
                schema: "games",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "ScenarioId",
                schema: "games",
                table: "Characters");
        }
    }
}