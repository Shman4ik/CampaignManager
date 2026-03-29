using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class PendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLovecraftian",
                schema: "games",
                table: "Occupations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsModern",
                schema: "games",
                table: "Occupations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SocialSkillSlots",
                schema: "games",
                table: "Occupations",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLovecraftian",
                schema: "games",
                table: "Occupations");

            migrationBuilder.DropColumn(
                name: "IsModern",
                schema: "games",
                table: "Occupations");

            migrationBuilder.DropColumn(
                name: "SocialSkillSlots",
                schema: "games",
                table: "Occupations");
        }
    }
}
