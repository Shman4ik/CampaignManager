using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class FixScenarioDeletiong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Scenarios_ScenarioId",
                schema: "games",
                table: "Characters");

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Scenarios_ScenarioId",
                schema: "games",
                table: "Characters",
                column: "ScenarioId",
                principalSchema: "games",
                principalTable: "Scenarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Scenarios_ScenarioId",
                schema: "games",
                table: "Characters");

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Scenarios_ScenarioId",
                schema: "games",
                table: "Characters",
                column: "ScenarioId",
                principalSchema: "games",
                principalTable: "Scenarios",
                principalColumn: "Id");
        }
    }
}
