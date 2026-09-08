using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class WeaponRarityFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRare",
                schema: "games",
                table: "Weapons",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                comment: "Редкое оружие: колонка «Встречается» таблицы XVII");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRare",
                schema: "games",
                table: "Weapons");
        }
    }
}
