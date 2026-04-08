using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class SkillEraAndCampaignEra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Is1920",
                schema: "games",
                table: "Skills",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsModern",
                schema: "games",
                table: "Skills",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "Era",
                schema: "games",
                table: "Campaigns",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Is1920",
                schema: "games",
                table: "Skills");

            migrationBuilder.DropColumn(
                name: "IsModern",
                schema: "games",
                table: "Skills");

            migrationBuilder.DropColumn(
                name: "Era",
                schema: "games",
                table: "Campaigns");
        }
    }
}
